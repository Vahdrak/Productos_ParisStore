using ComprasParaguai.Core.Interfaces;
using ComprasParaguai.Core.Models;
using Microsoft.Extensions.Logging;
using Polly;
using System.Net.Http.Json;
using System.Text.Json;

namespace ComprasParaguai.Infrastructure.Clients;

/// <summary>
/// Cliente HTTP tipado para Compras Paraguai API.
/// Usa Polly para reintentos, timeout y circuit breaker.
/// Token va en el header "token" (nunca en URL).
/// </summary>
public class ComprasParaguaiHttpClient : IComprasParaguaiClient, IProductQueryClient, IProductListClient
{
    private readonly HttpClient _httpClient;
    private readonly string _token;
    private readonly ILogger<ComprasParaguaiHttpClient> _logger;
    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;

    private const int MaxProductsPerBatch = 500;
    private const int TimeoutSeconds = 30;

    public ComprasParaguaiHttpClient(
        HttpClient httpClient,
        string baseUrl,
        string token,
        ILogger<ComprasParaguaiHttpClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _token = token ?? throw new ArgumentNullException(nameof(token));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Configurar cliente base.
        _httpClient.BaseAddress = new Uri(baseUrl ?? throw new ArgumentNullException(nameof(baseUrl)));
        // La API de Compras autentica con un header propio "token" (ver docs/api/*.yaml).
        // NO usar Authorization: Bearer — su backend lo interpreta como JWT y lo rechaza.
        _httpClient.DefaultRequestHeaders.Add("token", _token);
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        _httpClient.Timeout = TimeSpan.FromSeconds(TimeoutSeconds);

        // Política Polly: reintentos exponenciales + timeout.
        _retryPolicy = Policy
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .OrResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode && (int)r.StatusCode >= 500)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)), // 2s, 4s, 8s
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        "Reintento {RetryCount} en {DelayMs}ms. Razón: {Reason}",
                        retryCount, timespan.TotalMilliseconds, outcome.Exception?.Message ?? "HTTP 5xx");
                })
            .WrapAsync(
                Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(TimeoutSeconds)));
    }

    /// <summary>
    /// Importa un lote de productos (≤ 500).
    /// POST /api/products/import/ con retry policy.
    /// </summary>
    public async Task<ImportResponse> ImportProductsAsync(
        IEnumerable<ProductoApi> products,
        CancellationToken cancellationToken = default)
    {
        var productList = products.ToList();
        if (!productList.Any())
        {
            _logger.LogWarning("Intento de importar lote vacío");
            return new ImportResponse { Success = false, Message = "Lote vacío" };
        }

        if (productList.Count > MaxProductsPerBatch)
        {
            _logger.LogError(
                "Lote excede límite: {Count} > {Max}",
                productList.Count, MaxProductsPerBatch);
            throw new InvalidOperationException($"Máximo {MaxProductsPerBatch} productos por lote");
        }

        try
        {
            // Crear payload: array JSON con estructura de la API.
            var payload = productList.Select(p => new
            {
                code = p.Code,
                name = new { pt = p.NamePt, es = p.NameEs },
                price = p.RegimenTurismo && p.PrecioExenta.HasValue ? p.PrecioExenta.Value : p.Price,
                price_iva = p.PriceIva,
                stock = p.Stock,
                brand = p.Brand,
                description = new { pt = p.DescriptionPt, es = p.DescriptionEs },
                url_image = p.UrlImage,
                link = p.Link,
                link_purchase = p.LinkPurchase,
                force_image_update = p.ForceImageUpdate
            }).ToList();

            _logger.LogInformation("Enviando lote de {Count} productos a Compras Paraguai", productList.Count);

            // POST con retry policy.
            var response = await _retryPolicy.ExecuteAsync(async ct =>
                await _httpClient.PostAsJsonAsync("/api/products/import/", payload, cancellationToken: ct),
                cancellationToken);

            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            // 207 Multi-Status: el lote se procesó; el detalle por producto viene en validation_errors.
            if (response.StatusCode == System.Net.HttpStatusCode.MultiStatus || response.IsSuccessStatusCode)
            {
                var content = JsonSerializer.Deserialize<ImportResponse>(body);
                if (content != null)
                {
                    if (content.ProductsFailed > 0)
                    {
                        var sample = content.ValidationErrors
                            .Take(3)
                            .Select(e => $"{e.Product?.Code}: {string.Join("; ", e.Errors.Select(x => $"{x.Field} - {x.Message}"))}");
                        _logger.LogWarning(
                            "Lote procesado con rechazos: {Processed} OK, {Failed} rechazados. Ej.: {Sample}",
                            content.ProductsProcessed, content.ProductsFailed, string.Join(" | ", sample));
                    }
                    else
                    {
                        _logger.LogInformation("Lote procesado: {Processed} OK", content.ProductsProcessed);
                    }
                    return content;
                }
            }

            _logger.LogError(
                "Error importando lote. Status: {StatusCode}. Content: {Content}",
                response.StatusCode, body.Length > 2000 ? body[..2000] : body);

            return new ImportResponse
            {
                Success = false,
                Message = $"HTTP {response.StatusCode}: {body}",
                StatusCode = (int)response.StatusCode,
                RateLimited = response.StatusCode == System.Net.HttpStatusCode.TooManyRequests
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excepción enviando lote a Compras Paraguai");
            throw;
        }
    }

    /// <summary>
    /// GET /api/products/list/?code=X. Devuelve el producto tal como lo tiene Compras, o null si no existe.
    /// </summary>
    public async Task<RemoteProduct?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var response = await _retryPolicy.ExecuteAsync(async ct =>
            await _httpClient.GetAsync($"/api/products/list/?code={Uri.EscapeDataString(code)}", ct),
            cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("GET {Code} → HTTP {Status}: {Body}", code, response.StatusCode, body.Length > 300 ? body[..300] : body);
            return null;
        }

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return null;

        // La búsqueda por code puede ser "contiene"; quedarse con la coincidencia exacta.
        foreach (var item in data.EnumerateArray())
        {
            if (item.TryGetProperty("code", out var c) && c.GetString() == code)
                return ToRemote(item, code);
        }

        return null;
    }

    /// <summary>
    /// GET /api/products/list/ recorriendo todas las páginas (100 por página) con los filtros dados.
    /// Ante un 429 espera un minuto y reintenta la misma página.
    /// </summary>
    public async Task<List<RemoteProduct>> ListAsync(bool? available, bool? withStock, int delayMs, CancellationToken cancellationToken = default)
    {
        var items = new List<RemoteProduct>();
        for (var page = 1; ; page++)
        {
            using var doc = await GetListPageAsync(available, withStock, page, cancellationToken);
            var root = doc.RootElement;
            if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in data.EnumerateArray())
                {
                    var code = Str(item, "code");
                    if (code is not null) items.Add(ToRemote(item, code));
                }
            }

            var hasNext = root.TryGetProperty("has_next", out var next) && next.ValueKind == JsonValueKind.True;
            if (!hasNext) return items;
            if (delayMs > 0) await Task.Delay(delayMs, cancellationToken);
        }
    }

    /// <summary>Total de productos que cumplen el filtro (campo total_items de la primera página).</summary>
    public async Task<int> CountAsync(bool? available, bool? withStock, CancellationToken cancellationToken = default)
    {
        using var doc = await GetListPageAsync(available, withStock, 1, cancellationToken);
        return doc.RootElement.TryGetProperty("total_items", out var t) && t.ValueKind == JsonValueKind.Number ? t.GetInt32() : 0;
    }

    private const int MaxReintentos429 = 5;

    private async Task<JsonDocument> GetListPageAsync(bool? available, bool? withStock, int page, CancellationToken cancellationToken)
    {
        var query = new List<string> { $"page={page}" };
        if (available.HasValue) query.Add($"available={(available.Value ? "true" : "false")}");
        if (withStock.HasValue) query.Add($"with_stock={(withStock.Value ? "true" : "false")}");
        var url = "/api/products/list/?" + string.Join('&', query);

        for (var intento = 1; ; intento++)
        {
            var response = await _retryPolicy.ExecuteAsync(async ct => await _httpClient.GetAsync(url, ct), cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode) return JsonDocument.Parse(body);

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests && intento < MaxReintentos429)
            {
                _logger.LogWarning("GET lista página {Page} → 429. Espero 60 s (intento {N}/{Max})", page, intento, MaxReintentos429);
                await Task.Delay(TimeSpan.FromSeconds(60), cancellationToken);
                continue;
            }

            throw new HttpRequestException($"GET {url} → HTTP {(int)response.StatusCode}: {(body.Length > 300 ? body[..300] : body)}");
        }
    }

    private static RemoteProduct ToRemote(JsonElement item, string code) =>
        new(
            Code: code,
            NamePt: Str(item, "name_pt"),
            Price: Dec(item, "price"),
            PriceIva: Dec(item, "price_iva"),
            Stock: Dec(item, "stock"),
            Available: item.TryGetProperty("available", out var a) && a.ValueKind is JsonValueKind.True or JsonValueKind.False ? a.GetBoolean() : null,
            Link: Str(item, "link"),
            UpdatedAt: FechaParaguayAUtc(Str(item, "updated_at")));

    /// <summary>
    /// Compras devuelve las fechas sin zona y en hora de Paraguay (UTC-3 fijo desde octubre de 2024; mismo criterio que el panel).
    /// Se pasan a UTC, como el resto de la base.
    /// </summary>
    private static DateTime? FechaParaguayAUtc(string? s) =>
        DateTime.TryParse(s, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var dt)
            ? DateTime.SpecifyKind(dt.AddHours(3), DateTimeKind.Utc)
            : null;

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static decimal? Dec(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : null;

    /// <summary>
    /// Health check: GET /api/products/list/ con token.
    /// </summary>
    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _retryPolicy.ExecuteAsync(async ct =>
                await _httpClient.GetAsync("/api/products/list/", cancellationToken: ct),
                cancellationToken);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Health check falló");
            return false;
        }
    }
}

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
public class ComprasParaguaiHttpClient : IComprasParaguaiClient
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
                price = p.Price,
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

            // Parsear respuesta (esperamos 207 Multi-Status).
            if (response.StatusCode == System.Net.HttpStatusCode.MultiStatus || response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadFromJsonAsync<ImportResponse>();
                if (content != null)
                {
                    _logger.LogInformation(
                        "Importación OK: {Processed} procesados, {Failed} fallidos, {Errors} errores de validación",
                        content.ProductsProcessed, content.ProductsFailed, content.ValidationErrors.Count);
                    return content;
                }
            }

            // Error: log y retornar fallo.
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "Error importando lote. Status: {StatusCode}. Content: {Content}",
                response.StatusCode, errorContent);

            return new ImportResponse
            {
                Success = false,
                Message = $"HTTP {response.StatusCode}: {errorContent}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excepción enviando lote a Compras Paraguai");
            throw;
        }
    }

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

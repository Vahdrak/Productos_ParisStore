using ComprasParaguai.Core.Interfaces;
using ComprasParaguai.Core.Models;
using Microsoft.Extensions.Logging;
using Polly;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ComprasParaguai.Infrastructure.Clients;

/// <summary>
/// Cliente HTTP para Compara Paraguai API (servicio diferente de Compras Paraguai).
/// Usa la misma estructura que SyncComparaParaguaiService (que funciona en --test-batch).
/// Token va en header Authorization: Bearer {token}.
/// Implementa IComprasParaguaiClient para integración con SendService.
/// </summary>
public class ComparaParaguaiHttpClient : IComprasParaguaiClient
{
    private readonly HttpClient _httpClient;
    private readonly string _token;
    private readonly ILogger<ComparaParaguaiHttpClient> _logger;
    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;

    private const string ApiUrl = "https://comparaparaguai.com/api/integracao/produtos";
    private const int MaxProductsPerBatch = 1000;
    private const int TimeoutSeconds = 30;

    public ComparaParaguaiHttpClient(
        HttpClient httpClient,
        string token,
        ILogger<ComparaParaguaiHttpClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _token = token ?? throw new ArgumentNullException(nameof(token));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

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
    /// Implementa IComprasParaguaiClient.ImportProductsAsync para Compara Paraguai.
    /// Envía un lote de productos (≤ 500) con POST /produtos.
    /// </summary>
    public async Task<ImportResponse> ImportProductsAsync(
        IEnumerable<ProductoApi> products,
        CancellationToken cancellationToken = default)
    {
        var productList = products.ToList();
        if (!productList.Any())
        {
            _logger.LogWarning("Intento de enviar lote vacío a Compara Paraguai");
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
            // Convertir a DTO con snake_case (como en SyncComparaParaguaiService).
            var dtos = productList.Select(p => new ProductoComparaParaguaiDto
            {
                Code = p.Code,
                NameEs = p.NameEs,
                NamePt = p.NamePt,
                Price = p.Price,
                PriceIva = p.PriceIva,
                Stock = p.Stock,
                Brand = p.Brand,
                DescriptionEs = p.DescriptionEs,
                DescriptionPt = p.DescriptionPt,
                UrlImage = p.UrlImage,
                Link = p.Link,
                LinkPurchase = p.LinkPurchase
            }).ToList();

            var payload = new { produtos = dtos };
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl)
            {
                Content = content
            };
            request.Headers.Add("Authorization", $"Bearer {_token}");

            _logger.LogInformation("Enviando lote de {Count} productos a Compara Paraguai", productList.Count);

            var response = await _retryPolicy.ExecuteAsync(async ct =>
                await _httpClient.SendAsync(request, cancellationToken: ct),
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("✓ Lote enviado a Compara Paraguai. Status: {StatusCode}", response.StatusCode);
                return new ImportResponse { Success = true, Message = "OK", ProductsProcessed = productList.Count };
            }

            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "Error enviando a Compara Paraguai. Status: {StatusCode}. Content: {Content}",
                response.StatusCode, errorContent);

            return new ImportResponse
            {
                Success = false,
                Message = $"HTTP {response.StatusCode}: {errorContent}",
                ProductsFailed = productList.Count
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excepción enviando lote a Compara Paraguai");
            throw;
        }
    }

    /// <summary>
    /// Health check: GET /productos endpoint con token.
    /// </summary>
    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, ApiUrl);
            request.Headers.Add("Authorization", $"Bearer {_token}");

            var response = await _retryPolicy.ExecuteAsync(async ct =>
                await _httpClient.SendAsync(request, cancellationToken: ct),
                cancellationToken);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Health check a Compara Paraguai falló");
            return false;
        }
    }
}

/// <summary>
/// DTO para serialización de producto a Compara Paraguai (snake_case).
/// </summary>
public class ProductoComparaParaguaiDto
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = "";

    [JsonPropertyName("name_es")]
    public string NameEs { get; set; } = "";

    [JsonPropertyName("name_pt")]
    public string NamePt { get; set; } = "";

    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    [JsonPropertyName("price_iva")]
    public decimal? PriceIva { get; set; }

    [JsonPropertyName("stock")]
    public int Stock { get; set; }

    [JsonPropertyName("brand")]
    public string? Brand { get; set; }

    [JsonPropertyName("description_es")]
    public string DescriptionEs { get; set; } = "";

    [JsonPropertyName("description_pt")]
    public string DescriptionPt { get; set; } = "";

    [JsonPropertyName("url_image")]
    public string UrlImage { get; set; } = "";

    [JsonPropertyName("link")]
    public string Link { get; set; } = "";

    [JsonPropertyName("link_purchase")]
    public string? LinkPurchase { get; set; }
}

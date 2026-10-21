using System.Text.Json.Serialization;

namespace ComprasParaguai.Core.Models;

/// <summary>
/// Respuesta de Compras Paraguai POST /api/products/import/ (HTTP 207 Multi-Status).
/// <c>Success</c> = el lote se procesó; los productos rechazados vienen en <c>ValidationErrors</c>.
/// Compara Paraguai no devuelve detalle por producto: solo Success.
/// </summary>
public class ImportResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("products_processed")]
    public int ProductsProcessed { get; set; }

    [JsonPropertyName("products_failed")]
    public int ProductsFailed { get; set; }

    [JsonPropertyName("validation_errors")]
    public List<ValidationErrorEntry> ValidationErrors { get; set; } = new();

    /// <summary>Código HTTP cuando la API respondió con error (0 si no aplica). Un 5xx de todo el lote suele ser un producto "venenoso".</summary>
    [JsonIgnore]
    public int StatusCode { get; set; }

    /// <summary>HTTP 429: la API pidió esperar. El lote no se procesó; conviene reintentar tras una pausa.</summary>
    [JsonIgnore]
    public bool RateLimited { get; set; }

    /// <summary>Código → "campo: mensaje" del primer error, para marcar en BD.</summary>
    public Dictionary<string, string> FailedProducts() =>
        ValidationErrors
            .Where(e => !string.IsNullOrEmpty(e.Product?.Code))
            .GroupBy(e => e.Product!.Code!)
            .ToDictionary(
                g => g.Key,
                g => string.Join("; ", g.SelectMany(e => e.Errors).Select(x => $"{x.Field}: {x.Message}")));
}

/// <summary>Un producto rechazado: el producto tal como se envió + lista de errores.</summary>
public class ValidationErrorEntry
{
    [JsonPropertyName("product")]
    public ValidationErrorProduct? Product { get; set; }

    [JsonPropertyName("errors")]
    public List<FieldError> Errors { get; set; } = new();
}

public class ValidationErrorProduct
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }
}

public class FieldError
{
    [JsonPropertyName("field")]
    public string? Field { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

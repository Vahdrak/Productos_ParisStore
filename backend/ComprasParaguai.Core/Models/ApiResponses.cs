namespace ComprasParaguai.Core.Models;

/// <summary>
/// Respuesta de Compras Paraguai POST /api/products/import/ (HTTP 207 Multi-Status).
/// </summary>
public class ImportResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public int ProductsProcessed { get; set; }
    public int ProductsFailed { get; set; }
    public List<ValidationError> ValidationErrors { get; set; } = new();

    /// <summary>HTTP 429: la API pidió esperar. El lote no se procesó; conviene reintentar tras una pausa.</summary>
    public bool RateLimited { get; set; }
}

/// <summary>
/// Error de validación de un producto específico.
/// </summary>
public class ValidationError
{
    public string? Code { get; set; }
    public string? Field { get; set; }
    public string? Error { get; set; }
}

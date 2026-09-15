using ComprasParaguai.Core.Models;

namespace ComprasParaguai.Core.Interfaces;

/// <summary>
/// Cliente tipado para la API de Compras Paraguai.
/// Maneja autenticación (token), batching (500 por lote), reintentos (Polly).
/// </summary>
public interface IComprasParaguaiClient
{
    /// <summary>
    /// Importa un lote de productos (máximo 500).
    /// Retorna resultado: success, processed count, failed count, validation errors.
    /// </summary>
    /// <param name="products">Lote de ProductoApi (≤ 500).</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>ImportResponse con detalles del resultado.</returns>
    Task<ImportResponse> ImportProductsAsync(IEnumerable<ProductoApi> products, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica disponibilidad de la API (GET /api/products/list/ con filtro vacío).
    /// Usado para health checks antes de sincronización.
    /// </summary>
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);
}

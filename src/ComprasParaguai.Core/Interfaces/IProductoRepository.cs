using ComprasParaguai.Core.Models;

namespace ComprasParaguai.Core.Interfaces;

/// <summary>
/// Repositorio de lectura/escritura de productos en la BD propia.
/// </summary>
public interface IProductoRepository
{
    /// <summary>
    /// Carga todos los (code, source_hash) actuales de la BD en una sola query.
    /// Optimizado: carga mínimo de datos para comparación en memoria.
    /// </summary>
    Task<Dictionary<string, string?>> LoadProductHashMapAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Guarda/actualiza un lote de ProductoApi en la BD (upsert).
    /// Usa COPY a staging + INSERT ... ON CONFLICT para rendimiento masivo.
    /// Marca todos como sync_status = Pendiente.
    /// Dispara el trigger de auditoría en los UPDATE.
    /// </summary>
    /// <param name="products">Productos a insertar/actualizar.</param>
    /// <param name="now">Timestamp para last_changed_at (en UPDATE) y created_at (en INSERT).</param>
    Task UpsertProductsAsync(IEnumerable<ProductoApi> products, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marca productos como no disponibles (baja): stock=0, sync_status=Pendiente.
    /// Usado en reconciliación diaria para bajas detectadas.
    /// </summary>
    Task MarkAsUnavailableAsync(IEnumerable<string> codes, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>
    /// Carga productos con sync_status = Pendiente (listos para enviar a la API).
    /// </summary>
    Task<List<ProductoApi>> LoadPendingProductsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Marca un lote de productos como Enviado (sync_status = 1, last_sent_at = now).
    /// </summary>
    Task MarkAsSentAsync(IEnumerable<string> codes, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marca un producto como Error (sync_status = 2, last_error = mensaje).
    /// </summary>
    Task MarkAsErrorAsync(string code, string errorMessage, CancellationToken cancellationToken = default);

    /// <summary>
    /// Carga un producto por código (valores actuales).
    /// </summary>
    Task<ProductoApi?> LoadProductByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Carga el historial de cambios de un producto (antes de cada UPDATE).
    /// Retorna lista ordenada por fecha descendente (más reciente primero).
    /// </summary>
    Task<List<ProductHistorialDto>> LoadProductHistorialAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene el último registro del historial de un producto (para comparación diff).
    /// </summary>
    Task<ProductHistorialDto?> GetLastHistorialAsync(string code, CancellationToken cancellationToken = default);
}

public class ProductHistorialDto
{
    public DateTime ChangedAt { get; set; }
    public string? NameEs { get; set; }
    public string? NamePt { get; set; }
    public decimal? Price { get; set; }
    public decimal? PriceIva { get; set; }
    public int? Stock { get; set; }
    public string? Brand { get; set; }
    public string? UrlImage { get; set; }
}

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
    /// Marca todos como Pendiente en ambas columnas de estado (sync_status y sync_compara_status).
    /// Dispara el trigger de auditoría en los UPDATE.
    /// </summary>
    /// <param name="products">Productos a insertar/actualizar.</param>
    /// <param name="now">Timestamp para last_changed_at (en UPDATE) y created_at (en INSERT).</param>
    Task UpsertProductsAsync(IEnumerable<ProductoApi> products, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marca productos como no disponibles (baja): stock=0, Pendiente en ambas columnas de estado.
    /// Usado en reconciliación diaria para bajas detectadas.
    /// </summary>
    Task MarkAsUnavailableAsync(IEnumerable<string> codes, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>
    /// Carga TODOS los productos de la BD (sin filtrar por estado).
    /// Usado para el envío inicial completo a un destino.
    /// </summary>
    Task<List<ProductoApi>> LoadAllProductsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Carga N productos al azar (para envíos de prueba).
    /// </summary>
    Task<List<ProductoApi>> LoadRandomProductsAsync(int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Carga productos Pendiente (status = 0) para el destino indicado.
    /// </summary>
    Task<List<ProductoApi>> LoadPendingProductsAsync(SyncTarget target = SyncTarget.ComprasParaguai, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cuenta productos Pendiente (status = 0) para el destino indicado.
    /// </summary>
    Task<int> CountPendingProductsAsync(SyncTarget target = SyncTarget.ComprasParaguai, CancellationToken cancellationToken = default);

    /// <summary>
    /// Carga productos en Error (status = 2) para el destino indicado, con mensaje de error.
    /// </summary>
    Task<List<ProductoWithErrorDto>> LoadErrorProductsAsync(SyncTarget target = SyncTarget.ComprasParaguai, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marca un lote como Enviado (status = 1, sent_at = now) en la columna del destino.
    /// </summary>
    Task MarkAsSentAsync(IEnumerable<string> codes, DateTime now, SyncTarget target = SyncTarget.ComprasParaguai, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marca un producto como Error (status = 2, error = mensaje) en la columna del destino.
    /// </summary>
    Task MarkAsErrorAsync(string code, string errorMessage, SyncTarget target = SyncTarget.ComprasParaguai, CancellationToken cancellationToken = default);

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

    /// <summary>
    /// Busca y pagina productos en la BD.
    /// </summary>
    Task<ProductListDto> SearchProductsAsync(string? code, string? name, int? syncStatus, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene los cambios recientes de todos los productos con paginación.
    /// </summary>
    Task<ProductChangeListDto> GetRecentChangesAsync(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
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

public class ProductChangeDto
{
    public string Code { get; set; } = string.Empty;
    public string? ProductName { get; set; }
    public string Field { get; set; } = string.Empty;
    public string? Before { get; set; }
    public string? After { get; set; }
    public DateTime ChangedAt { get; set; }
}

public class ProductChangeListDto
{
    public List<ProductChangeDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class ProductListDto
{
    public List<ProductoApi> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class ProductoWithErrorDto
{
    public string Code { get; set; } = string.Empty;
    public string? NameEs { get; set; }
    public string? NamePt { get; set; }
    public string? LastError { get; set; }
    public DateTime? LastErrorAt { get; set; }
}

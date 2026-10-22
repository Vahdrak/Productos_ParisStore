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
    /// <param name="contexto">Origen y usuario del cambio; quedan en productos_historial.</param>
    Task UpsertProductsAsync(IEnumerable<ProductoApi> products, DateTime now, CambioContexto contexto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marca productos como no disponibles (baja): stock=0, Pendiente en ambas columnas de estado.
    /// Usado en reconciliación diaria para bajas detectadas.
    /// </summary>
    Task MarkAsUnavailableAsync(IEnumerable<string> codes, DateTime now, CambioContexto contexto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mueve al archivo el historial de cambios más viejo que N meses. Devuelve cuántas filas movió.
    /// </summary>
    Task<long> ArchivarHistorialAsync(int meses, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recalcula marcas.activo y categorias.activo (una marca/nodo está activo si tiene productos,
    /// propios o de sus descendientes). Nunca borra: así se conservan los flags si vuelve a aparecer.
    /// Se llama desde la reconciliación diaria.
    /// </summary>
    Task RefreshCatalogoActivoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Carga todos los productos que se pueden enviar al destino (sin filtrar por estado, pero sin los retenidos
    /// por una pausa o excepción). Usado para el envío inicial completo.
    /// </summary>
    Task<List<ProductoApi>> LoadAllProductsAsync(SyncTarget target, CancellationToken cancellationToken = default);

    /// <summary>
    /// Carga N productos al azar enviables al destino (para envíos de prueba).
    /// </summary>
    Task<List<ProductoApi>> LoadRandomProductsAsync(int count, SyncTarget target, CancellationToken cancellationToken = default);

    /// <summary>
    /// Carga N productos al azar marcados como Enviado (status = 1) para el destino (para verificar envíos).
    /// </summary>
    Task<List<ProductoApi>> LoadRandomSentProductsAsync(int count, SyncTarget target, CancellationToken cancellationToken = default);

    /// <summary>
    /// Carga productos Pendiente (status = 0) del destino que se pueden enviar: excluye los retenidos
    /// por una pausa o excepción activa.
    /// </summary>
    Task<List<ProductoApi>> LoadPendingProductsAsync(SyncTarget target = SyncTarget.ComprasParaguai, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cuenta productos Pendiente (status = 0) enviables del destino (sin los retenidos).
    /// </summary>
    Task<int> CountPendingProductsAsync(SyncTarget target = SyncTarget.ComprasParaguai, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pendientes del destino paginados en el servidor, indicando cuáles están retenidos y por qué regla.
    /// Es lo que muestra la pestaña Pendientes (no se usa para enviar).
    /// </summary>
    Task<ProductoPendienteListDto> BuscarPendientesAsync(SyncTarget target, PendientesFiltro filtro, CancellationToken cancellationToken = default);

    /// <summary>
    /// Productos ya publicados en el destino que una pausa con "retirar del destino" cubre y que todavía
    /// no se retiraron. Se envían con stock 0.
    /// </summary>
    Task<List<ProductoApi>> LoadParaRetirarAsync(SyncTarget target, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marca como retirados del destino (ya se les envió stock 0) los productos indicados.
    /// </summary>
    Task MarkRetiradosAsync(IEnumerable<string> codes, SyncTarget target, CancellationToken cancellationToken = default);

    /// <summary>
    /// Los productos retirados que ya no están retenidos por nada vuelven a Pendiente para reenviarse con su
    /// stock real. Devuelve cuántos se liberaron.
    /// </summary>
    Task<int> LiberarRetiradosAsync(SyncTarget target, CancellationToken cancellationToken = default);

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
    /// Registra que falló el RETIRO del destino (stock 0) de un producto; queda en Error con etapa "retiro".
    /// </summary>
    Task MarkRetiroErrorAsync(string code, string errorMessage, SyncTarget target, CancellationToken cancellationToken = default);

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
    /// Busca y pagina productos en la BD. <paramref name="categoriaId"/> incluye todo el subárbol de esa categoría.
    /// </summary>
    Task<ProductListDto> SearchProductsAsync(string? code, string? name, int? syncStatus, int page, int pageSize, SyncTarget target = SyncTarget.ComprasParaguai, int? marcaId = null, int? categoriaId = null, bool? regimen = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Vuelve a Pendiente (para reenviar) los productos Enviados o en Error de los destinos indicados: los códigos dados
    /// o todos los que cumplan el filtro. Devuelve cuántos cambió.
    /// </summary>
    Task<int> ReenviarAsync(IReadOnlyCollection<SyncTarget> targets, ProductosFiltro filtro, IReadOnlyCollection<string>? codes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cuenta productos por estado (Pendiente/Enviado/Error) para cada destino, con fecha del último envío.
    /// </summary>
    Task<List<TargetStatusCounts>> GetStatusCountsAsync(CancellationToken cancellationToken = default);

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

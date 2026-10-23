using ComprasParaguai.Core.Models;

namespace ComprasParaguai.Core.Interfaces;

/// <summary>
/// Foto de los productos con stock que Compras Paraguai marca como no disponibles (available = false).
/// La toma Worker.Verify; la API solo la lee.
/// </summary>
public interface IDisponibilidadRepository
{
    /// <summary>Reemplaza la foto completa en una transacción y guarda cuándo se tomó.</summary>
    Task ReemplazarAsync(IReadOnlyList<RemoteProduct> noDisponibles, int totalConStock, DateTime checkedAt, CancellationToken cancellationToken = default);

    /// <summary>Página de la foto, unida a los datos locales del producto. Solo los que tienen stock local.</summary>
    Task<NoDisponiblesDto> ListarAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Todos los códigos de la foto (son pocos): sirve para marcar las filas de Productos.</summary>
    Task<List<string>> CodigosAsync(CancellationToken cancellationToken = default);
}

public class NoDisponibleItemDto
{
    public string Code { get; set; } = "";
    public string? NamePt { get; set; }
    public string? Marca { get; set; }
    public decimal? Price { get; set; }
    public int StockLocal { get; set; }
    public decimal? StockApi { get; set; }
    /// <summary>Última actualización del producto en Compras, en UTC.</summary>
    public DateTime? UpdatedAtApi { get; set; }
    public string? Link { get; set; }
}

public class NoDisponiblesDto
{
    public List<NoDisponibleItemDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    /// <summary>Cuándo se tomó la foto (UTC). Null si todavía no se corrió <c>Worker.Verify --disponibilidad</c>.</summary>
    public DateTime? CheckedAt { get; set; }
    /// <summary>Productos con stock que tenía Compras al tomar la foto.</summary>
    public int TotalConStock { get; set; }
}

namespace ComprasParaguai.Core.Interfaces;

/// <summary>
/// Lectura del catálogo normalizado: árbol de categorías y marcas, con conteos de productos.
/// Base para la pantalla de filtros de envío.
/// </summary>
public interface ICatalogoRepository
{
    /// <summary>
    /// Árbol completo de categorías (raíz → hojas). Los conteos de cada nodo incluyen
    /// los productos de todos sus descendientes.
    /// </summary>
    Task<List<CategoriaNodoDto>> GetArbolCategoriasAsync(bool soloActivas = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marcas con su conteo de productos, ordenadas por cantidad, con búsqueda por nombre y paginación.
    /// </summary>
    Task<MarcaListDto> GetMarcasAsync(string? search, bool soloActivas, int page, int pageSize, CancellationToken cancellationToken = default);
}

public class CategoriaNodoDto
{
    public int Id { get; set; }
    public int? ParentId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int Nivel { get; set; }
    public string Ruta { get; set; } = string.Empty;
    public bool Activo { get; set; }

    /// <summary>Productos del nodo y de todos sus descendientes.</summary>
    public int Productos { get; set; }
    public int PendientesCompras { get; set; }
    public int PendientesCompara { get; set; }

    /// <summary>Id de la pausa activa de ESTE nodo (solo categoría) por destino; null si no está pausado.</summary>
    public int? PausaIdCompras { get; set; }
    public int? PausaIdCompara { get; set; }

    /// <summary>Está pausado porque un ancestro tiene una pausa de categoría activa.</summary>
    public bool HeredadaCompras { get; set; }
    public bool HeredadaCompara { get; set; }

    public List<CategoriaNodoDto> Hijos { get; set; } = new();
}

public class MarcaDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; }
    public int Productos { get; set; }
    public int PendientesCompras { get; set; }
    public int PendientesCompara { get; set; }

    /// <summary>Id de la pausa activa de ESTA marca (sola, sin categoría) por destino; null si no está pausada.</summary>
    public int? PausaIdCompras { get; set; }
    public int? PausaIdCompara { get; set; }
}

public class MarcaListDto
{
    public List<MarcaDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

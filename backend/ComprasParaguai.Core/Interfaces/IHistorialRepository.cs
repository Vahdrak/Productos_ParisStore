namespace ComprasParaguai.Core.Interfaces;

/// <summary>
/// Consulta del historial de cambios de productos (productos_historial), con filtros por
/// producto, marca, categoría, campo, origen y fechas.
/// </summary>
public interface IHistorialRepository
{
    Task<CambioListDto> BuscarCambiosAsync(CambiosFiltro filtro, CancellationToken cancellationToken = default);
}

public class CambiosFiltro
{
    /// <summary>Código exacto del producto.</summary>
    public string? Code { get; set; }

    /// <summary>Texto libre sobre código o nombre del producto.</summary>
    public string? Texto { get; set; }

    public int? MarcaId { get; set; }

    /// <summary>Categoría; incluye todo su subárbol.</summary>
    public int? CategoriaId { get; set; }

    /// <summary>Solo cambios que tocaron este campo (price, stock, name_es, marca, categoria, url_image...).</summary>
    public string? Campo { get; set; }

    /// <summary>sync | reconciliacion | api | manual | historico.</summary>
    public string? Origen { get; set; }

    public DateOnly? Desde { get; set; }
    public DateOnly? Hasta { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class CampoCambioDto
{
    public string Campo { get; set; } = string.Empty;
    public string? Antes { get; set; }
    public string? Despues { get; set; }
}

public class CambioDto
{
    public long Id { get; set; }
    public DateTime ChangedAt { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Nombre { get; set; }
    public string? Marca { get; set; }

    /// <summary>"Alta" (producto nuevo) o "Cambio".</summary>
    public string Operacion { get; set; } = "Cambio";
    public string? Origen { get; set; }
    public string? Usuario { get; set; }
    public List<CampoCambioDto> Cambios { get; set; } = new();
}

public class CambioListDto
{
    public List<CambioDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

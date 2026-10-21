using ComprasParaguai.Core.Models;

namespace ComprasParaguai.Core.Interfaces;

/// <summary>
/// Productos en estado Error de un destino, agrupados por tipo de error, marca y departamento,
/// y reintento (volver a Pendiente) de los que se elijan.
/// </summary>
public interface IErrorEnvioRepository
{
    Task<ErroresResultadoDto> BuscarAsync(SyncTarget target, ErroresFiltro filtro, CancellationToken cancellationToken = default);

    /// <summary>
    /// Vuelve a Pendiente (y limpia el mensaje) los productos en Error que cumplan el filtro, o solo los códigos indicados.
    /// Devuelve cuántos se reiniciaron.
    /// </summary>
    Task<int> ReintentarAsync(SyncTarget target, ErroresFiltro filtro, IReadOnlyCollection<string>? codes, CancellationToken cancellationToken = default);
}

public class ErroresFiltro
{
    public int? MarcaId { get; set; }

    /// <summary>Categoría; incluye todo su subárbol.</summary>
    public int? CategoriaId { get; set; }

    /// <summary>Texto sobre código o nombre.</summary>
    public string? Texto { get; set; }

    /// <summary>Código de tipo de error (ver ErrorClasificador): duplicado, imagen, precio, codigo, credenciales, temporal, otro.</summary>
    public string? Tipo { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;

    /// <summary>True si no restringe nada (se usaría para "reintentar todo").</summary>
    public bool SinFiltros => MarcaId is null && CategoriaId is null && string.IsNullOrWhiteSpace(Texto) && string.IsNullOrWhiteSpace(Tipo);
}

public class ErrorTipoResumenDto
{
    public string Codigo { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string QueHacer { get; set; } = string.Empty;
    public bool Reintentable { get; set; }
    public int Total { get; set; }
}

public class ErrorGrupoDto
{
    public int? Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int Total { get; set; }
}

public class ErrorProductoDto
{
    public string Code { get; set; } = string.Empty;
    public string? Nombre { get; set; }
    public string? Marca { get; set; }

    /// <summary>Ruta de la categoría sin la empresa (COCINA > VAJILLAS > ...).</summary>
    public string? Categoria { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string TipoTitulo { get; set; } = string.Empty;

    /// <summary>"envio" o "retiro".</summary>
    public string Etapa { get; set; } = "envio";
    public string? Mensaje { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ErroresResultadoDto
{
    /// <summary>Resumen por tipo de error (sin aplicar el filtro de tipo, para poder cambiar de tipo).</summary>
    public List<ErrorTipoResumenDto> Tipos { get; set; } = new();

    /// <summary>Departamentos con errores (sin aplicar el filtro de categoría).</summary>
    public List<ErrorGrupoDto> PorDepartamento { get; set; } = new();

    /// <summary>Marcas con más errores (las 20 primeras; sin aplicar el filtro de marca).</summary>
    public List<ErrorGrupoDto> PorMarca { get; set; } = new();

    public List<ErrorProductoDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

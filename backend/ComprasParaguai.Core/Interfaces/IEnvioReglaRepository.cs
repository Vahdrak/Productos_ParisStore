using ComprasParaguai.Core.Models;

namespace ComprasParaguai.Core.Interfaces;

/// <summary>
/// Reglas de envío por destino: pausas (por marca, categoría o ambas) y excepciones por producto (SKU),
/// con su historial. Las escrituras reciben el contexto (usuario) para dejar quién las hizo.
/// </summary>
public interface IEnvioReglaRepository
{
    Task<List<EnvioPausaDto>> ListarPausasAsync(SyncTarget target, CancellationToken cancellationToken = default);

    /// <summary>Cuántos productos cubriría una pausa con esa marca y/o categoría (vista previa antes de crearla).</summary>
    Task<EnvioImpactoDto> ImpactoAsync(SyncTarget target, int? marcaId, int? categoriaId, CancellationToken cancellationToken = default);

    /// <summary>Crea la pausa; si ya existía esa misma regla, la reactiva y actualiza motivo/retiro.</summary>
    Task<EnvioPausaDto> CrearPausaAsync(CrearPausaRequest request, CambioContexto contexto, CancellationToken cancellationToken = default);

    /// <summary>Activa/desactiva o edita una pausa. False si no existe.</summary>
    Task<bool> ActualizarPausaAsync(int id, bool? activo, string? motivo, bool? retirarDelDestino, CambioContexto contexto, CancellationToken cancellationToken = default);

    Task<bool> BorrarPausaAsync(int id, CambioContexto contexto, CancellationToken cancellationToken = default);

    /// <summary>Estado de envío del producto en cada destino: excepción propia, si está retenido y por qué.</summary>
    Task<List<EnvioEstadoProductoDto>?> EstadoProductoAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>Crea o cambia la excepción del producto en el destino. False si el producto no existe.</summary>
    Task<bool> GuardarExcepcionAsync(string code, SyncTarget target, string accion, string? motivo, CambioContexto contexto, CancellationToken cancellationToken = default);

    Task<bool> BorrarExcepcionAsync(string code, SyncTarget target, CambioContexto contexto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pone o quita la excepción de muchos productos a la vez: los códigos dados y/o los que cumplan el filtro de la lista.
    /// <paramref name="accion"/> "retener" o "enviar"; null quita la excepción (vuelve a heredar las pausas).
    /// Solo toca lo que cambia (no reescribe excepciones iguales). Devuelve cuántas excepciones cambió.
    /// </summary>
    Task<int> AplicarExcepcionLoteAsync(IReadOnlyCollection<SyncTarget> targets, ProductosFiltro filtro, IReadOnlyCollection<string>? codes,
        string? accion, string? motivo, CambioContexto contexto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Todo lo que hoy NO se envía al destino (por una pausa o por una excepción "retener"), en cualquier estado de envío.
    /// <paramref name="origen"/>: "pausa", "excepcion" o null (todos).
    /// </summary>
    Task<RetenidosListDto> BuscarRetenidosAsync(SyncTarget target, string? origen, string? q, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<ReglaHistorialListDto> HistorialAsync(SyncTarget? target, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Departamentos y secciones donde la marca tiene productos (para pausar "marca en categoría").</summary>
    Task<List<MarcaCategoriaDto>> CategoriasDeMarcaAsync(int marcaId, SyncTarget target, CancellationToken cancellationToken = default);
}

public class CrearPausaRequest
{
    public SyncTarget Target { get; set; }
    public int? MarcaId { get; set; }
    public int? CategoriaId { get; set; }
    public string? Motivo { get; set; }
    public bool RetirarDelDestino { get; set; }
}

public class EnvioPausaDto
{
    public int Id { get; set; }
    public string Target { get; set; } = string.Empty;
    public int? MarcaId { get; set; }
    public string? Marca { get; set; }
    public int? CategoriaId { get; set; }
    public string? CategoriaRuta { get; set; }
    public bool RetirarDelDestino { get; set; }
    public bool Activo { get; set; }
    public string? Motivo { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>Productos que cubre la pausa (con o sin estar activa).</summary>
    public int Productos { get; set; }
}

public class EnvioImpactoDto
{
    public int Productos { get; set; }

    /// <summary>De ellos, pendientes de envío al destino.</summary>
    public int Pendientes { get; set; }

    /// <summary>De ellos, ya publicados en el destino (los que "retirar del destino" pondría en stock 0).</summary>
    public int Publicados { get; set; }
}

public class EnvioExcepcionDto
{
    public string Accion { get; set; } = string.Empty;
    public bool Activo { get; set; }
    public string? Motivo { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class RetenidoDto
{
    public string Code { get; set; } = string.Empty;
    public string? NamePt { get; set; }
    public string? Marca { get; set; }
    public string? CategoriaRuta { get; set; }
    public int Stock { get; set; }

    /// <summary>Estado de envío al destino: 0 pendiente, 1 enviado, 2 error.</summary>
    public int Estado { get; set; }

    /// <summary>Qué lo retiene: "LACOSTE en INDUMENTARIA" o "Excepción: retener SKU".</summary>
    public string? RetenidoPor { get; set; }

    /// <summary>True si lo retiene una excepción propia del SKU (se quita); false si lo retiene una pausa.</summary>
    public bool PorExcepcion { get; set; }

    /// <summary>Motivo de la excepción, si la hay.</summary>
    public string? Motivo { get; set; }
}

public class RetenidosListDto
{
    public List<RetenidoDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }

    /// <summary>Del total del filtro de búsqueda (sin el de origen): cuántos retiene una pausa y cuántos una excepción.</summary>
    public int PorPausa { get; set; }
    public int PorExcepcion { get; set; }
}

public class EnvioEstadoProductoDto
{
    public string Target { get; set; } = string.Empty;
    public EnvioExcepcionDto? Excepcion { get; set; }
    public bool Retenido { get; set; }
    public string? RetenidoPor { get; set; }
    public bool Retirado { get; set; }
}

public class ReglaHistorialDto
{
    public long Id { get; set; }
    public string Tabla { get; set; } = string.Empty;
    public int ReglaId { get; set; }
    public string Accion { get; set; } = string.Empty;
    public string? Detalle { get; set; }
    public string? Usuario { get; set; }
    public DateTime ChangedAt { get; set; }
}

public class ReglaHistorialListDto
{
    public List<ReglaHistorialDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class MarcaCategoriaDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int Nivel { get; set; }
    public string Ruta { get; set; } = string.Empty;
    public int Productos { get; set; }

    /// <summary>Id de la pausa activa de "esta marca en esta categoría", si existe.</summary>
    public int? PausaId { get; set; }
}

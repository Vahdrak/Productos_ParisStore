using ComprasParaguai.Core.Interfaces;
using ComprasParaguai.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ComprasParaguai.Api.Controllers;

/// <summary>
/// Retención de productos en bloque: retener / forzar el envío / quitar la excepción de muchos SKUs a la vez
/// (los elegidos o todo lo que cumple el filtro de la lista) y ver todo lo que hoy no se envía. Leer es libre;
/// cambiar exige sesión y queda en el historial de reglas con el usuario.
/// </summary>
[ApiController]
[Route("api/envio")]
public class EnvioRetencionController : ControllerBase
{
    private readonly IEnvioReglaRepository _repository;
    private readonly ILogger<EnvioRetencionController> _logger;

    public EnvioRetencionController(IEnvioReglaRepository repository, ILogger<EnvioRetencionController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    private static SyncTarget? ParseTarget(string? target) => target?.ToLowerInvariant() switch
    {
        "compras" or "comprasparaguai" => SyncTarget.ComprasParaguai,
        "compara" or "comparaparaguai" => SyncTarget.ComparaParaguai,
        _ => null
    };

    /// <summary>GET /api/envio/retenidos?target=compras&amp;origen=pausa|excepcion&amp;q=lacoste&amp;page=1&amp;pageSize=50</summary>
    [HttpGet("retenidos")]
    public async Task<ActionResult<RetenidosListDto>> Retenidos(
        [FromQuery] string? target, [FromQuery] string? origen, [FromQuery] string? q,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        if (ParseTarget(target) is not { } t)
            return BadRequest(new { error = "target debe ser 'compras' o 'compara'" });
        try
        {
            return Ok(await _repository.BuscarRetenidosAsync(t, origen, q, page, pageSize, cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cargando los productos retenidos");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    public record LoteBody(
        string? Target, string? Accion, string? Motivo, string[]? Codes, string? Code, string? Name, int? SyncStatus,
        string? FiltroTarget, int? MarcaId, int? CategoriaId, bool? Regimen);

    /// <summary>
    /// POST /api/envio/excepciones/lote — "accion": "retener" | "enviar" | "quitar"; "target": "compras" | "compara" | "ambos".
    /// Se indican códigos o el filtro de la lista de productos (code, name, syncStatus + filtroTarget, marcaId, categoriaId, regimen);
    /// sin ninguno de los dos se rechaza (nunca se aplica a todo el catálogo por omisión).
    /// </summary>
    [HttpPost("excepciones/lote")]
    [Authorize]
    public async Task<IActionResult> Lote([FromBody] LoteBody body, CancellationToken cancellationToken)
    {
        var destinos = new List<SyncTarget>();
        switch (body.Target?.ToLowerInvariant())
        {
            case "compras": destinos.Add(SyncTarget.ComprasParaguai); break;
            case "compara": destinos.Add(SyncTarget.ComparaParaguai); break;
            case "ambos": destinos.Add(SyncTarget.ComprasParaguai); destinos.Add(SyncTarget.ComparaParaguai); break;
            default: return BadRequest(new { error = "target debe ser 'compras', 'compara' o 'ambos'" });
        }

        string? accion = body.Accion?.ToLowerInvariant() switch
        {
            "retener" => "retener",
            "enviar" => "enviar",
            "quitar" => null,
            _ => "invalida"
        };
        if (accion == "invalida")
            return BadRequest(new { error = "accion debe ser 'retener', 'enviar' o 'quitar'" });

        var filtro = new ProductosFiltro
        {
            Code = body.Code, Name = body.Name, SyncStatus = body.SyncStatus, Target = ParseTarget(body.FiltroTarget) ?? SyncTarget.ComprasParaguai,
            MarcaId = body.MarcaId, CategoriaId = body.CategoriaId, Regimen = body.Regimen
        };
        bool hayCodigos = body.Codes is { Length: > 0 };
        if (!hayCodigos && filtro.SinFiltros)
            return BadRequest(new { error = "Indica códigos o al menos un filtro (marca, categoría, código, régimen o estado)" });

        try
        {
            var contexto = new CambioContexto(OrigenCambio.Api, User.Identity?.Name);
            int n = await _repository.AplicarExcepcionLoteAsync(destinos, filtro, hayCodigos ? body.Codes : null, accion, body.Motivo, contexto, cancellationToken);
            _logger.LogInformation("{Usuario}: {Accion} en lote, {N} excepciones cambiadas en {Destinos}",
                User.Identity?.Name ?? "?", body.Accion, n, body.Target);
            return Ok(new { afectados = n });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error aplicando excepciones en lote");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

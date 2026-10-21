using ComprasParaguai.Core.Interfaces;
using ComprasParaguai.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ComprasParaguai.Api.Controllers;

/// <summary>
/// Pausas de envío por marca, categoría o ambas, por destino. Leer es libre; crear, cambiar y borrar exigen sesión.
/// </summary>
[ApiController]
[Route("api/envio-pausas")]
public class EnvioPausasController : ControllerBase
{
    private readonly IEnvioReglaRepository _repository;
    private readonly ILogger<EnvioPausasController> _logger;

    public EnvioPausasController(IEnvioReglaRepository repository, ILogger<EnvioPausasController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    private static bool TryParseTarget(string? target, out SyncTarget result)
    {
        switch (target?.ToLowerInvariant())
        {
            case "compras": case "comprasparaguai": result = SyncTarget.ComprasParaguai; return true;
            case "compara": case "comparaparaguai": result = SyncTarget.ComparaParaguai; return true;
            default: result = default; return false;
        }
    }

    private CambioContexto Contexto() => new(OrigenCambio.Api, User.Identity?.Name);

    /// <summary>GET /api/envio-pausas?target=compras</summary>
    [HttpGet]
    public async Task<ActionResult<List<EnvioPausaDto>>> GetAll([FromQuery] string? target, CancellationToken cancellationToken)
    {
        if (!TryParseTarget(target, out var t))
            return BadRequest(new { error = "target debe ser 'compras' o 'compara'" });
        try
        {
            return Ok(await _repository.ListarPausasAsync(t, cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listando pausas");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Vista previa: cuántos productos cubriría. GET /api/envio-pausas/impacto?target=&marcaId=&categoriaId=</summary>
    [HttpGet("impacto")]
    public async Task<ActionResult<EnvioImpactoDto>> Impacto(
        [FromQuery] string? target, [FromQuery] int? marcaId, [FromQuery] int? categoriaId, CancellationToken cancellationToken)
    {
        if (!TryParseTarget(target, out var t))
            return BadRequest(new { error = "target debe ser 'compras' o 'compara'" });
        if (marcaId is null && categoriaId is null)
            return BadRequest(new { error = "Indica marcaId, categoriaId o ambos" });
        try
        {
            return Ok(await _repository.ImpactoAsync(t, marcaId, categoriaId, cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculando el impacto de una pausa");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    public record CrearPausaBody(string? Target, int? MarcaId, int? CategoriaId, string? Motivo, bool RetirarDelDestino = false);

    /// <summary>Crea la pausa (o reactiva la misma regla si ya existía). POST /api/envio-pausas</summary>
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<EnvioPausaDto>> Crear([FromBody] CrearPausaBody body, CancellationToken cancellationToken)
    {
        if (!TryParseTarget(body.Target, out var t))
            return BadRequest(new { error = "target debe ser 'compras' o 'compara'" });
        if (body.MarcaId is null && body.CategoriaId is null)
            return BadRequest(new { error = "Indica marcaId, categoriaId o ambos" });
        try
        {
            var pausa = await _repository.CrearPausaAsync(new CrearPausaRequest
            {
                Target = t,
                MarcaId = body.MarcaId,
                CategoriaId = body.CategoriaId,
                Motivo = body.Motivo,
                RetirarDelDestino = body.RetirarDelDestino
            }, Contexto(), cancellationToken);
            return Ok(pausa);
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == Npgsql.PostgresErrorCodes.ForeignKeyViolation)
        {
            return BadRequest(new { error = "La marca o la categoría indicada no existe" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creando pausa");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    public record ActualizarPausaBody(bool? Activo, string? Motivo, bool? RetirarDelDestino);

    /// <summary>Activa/desactiva o edita una pausa. PATCH /api/envio-pausas/{id}</summary>
    [HttpPatch("{id:int}")]
    [Authorize]
    public async Task<IActionResult> Actualizar(int id, [FromBody] ActualizarPausaBody body, CancellationToken cancellationToken)
    {
        try
        {
            bool ok = await _repository.ActualizarPausaAsync(id, body.Activo, body.Motivo, body.RetirarDelDestino, Contexto(), cancellationToken);
            return ok ? NoContent() : NotFound(new { error = $"Pausa {id} no encontrada" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando pausa {Id}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>DELETE /api/envio-pausas/{id}</summary>
    [HttpDelete("{id:int}")]
    [Authorize]
    public async Task<IActionResult> Borrar(int id, CancellationToken cancellationToken)
    {
        try
        {
            bool ok = await _repository.BorrarPausaAsync(id, Contexto(), cancellationToken);
            return ok ? NoContent() : NotFound(new { error = $"Pausa {id} no encontrada" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error borrando pausa {Id}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Quién creó/cambió/activó/borró cada regla. GET /api/envio-pausas/historial?target=&amp;page=</summary>
    [HttpGet("historial")]
    public async Task<ActionResult<ReglaHistorialListDto>> Historial(
        [FromQuery] string? target, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        SyncTarget? t = null;
        if (!string.IsNullOrWhiteSpace(target))
        {
            if (!TryParseTarget(target, out var parsed))
                return BadRequest(new { error = "target debe ser 'compras' o 'compara'" });
            t = parsed;
        }
        try
        {
            return Ok(await _repository.HistorialAsync(t, page, pageSize, cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cargando historial de reglas");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

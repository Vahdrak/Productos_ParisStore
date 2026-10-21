using ComprasParaguai.Core.Interfaces;
using ComprasParaguai.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ComprasParaguai.Api.Controllers;

/// <summary>
/// Excepciones por producto (SKU): "enviar" aunque su marca o categoría esté pausada, o "retener" aunque no lo esté.
/// La excepción gana sobre las pausas. Leer es libre; cambiar exige sesión.
/// </summary>
[ApiController]
[Route("api/productos/{code}")]
public class EnvioExcepcionesController : ControllerBase
{
    private readonly IEnvioReglaRepository _repository;
    private readonly ILogger<EnvioExcepcionesController> _logger;

    public EnvioExcepcionesController(IEnvioReglaRepository repository, ILogger<EnvioExcepcionesController> logger)
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

    /// <summary>Estado de envío del producto en cada destino. GET /api/productos/{code}/envio</summary>
    [HttpGet("envio")]
    public async Task<ActionResult<List<EnvioEstadoProductoDto>>> Estado(string code, CancellationToken cancellationToken)
    {
        try
        {
            var estado = await _repository.EstadoProductoAsync(code, cancellationToken);
            return estado is null ? NotFound(new { error = $"Producto {code} no encontrado" }) : Ok(estado);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cargando el estado de envío de {Code}", code);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    public record ExcepcionBody(string? Accion, string? Motivo);

    /// <summary>PUT /api/productos/{code}/excepcion?target=compara  { "accion": "enviar" | "retener", "motivo": "..." }</summary>
    [HttpPut("excepcion")]
    [Authorize]
    public async Task<IActionResult> Guardar(string code, [FromQuery] string? target, [FromBody] ExcepcionBody body, CancellationToken cancellationToken)
    {
        if (!TryParseTarget(target, out var t))
            return BadRequest(new { error = "target debe ser 'compras' o 'compara'" });
        var accion = body.Accion?.ToLowerInvariant();
        if (accion is not ("enviar" or "retener"))
            return BadRequest(new { error = "accion debe ser 'enviar' o 'retener'" });
        try
        {
            bool ok = await _repository.GuardarExcepcionAsync(code, t, accion, body.Motivo, Contexto(), cancellationToken);
            return ok ? NoContent() : NotFound(new { error = $"Producto {code} no encontrado" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error guardando la excepción de {Code}", code);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Quita la excepción (el producto vuelve a seguir a sus pausas). DELETE /api/productos/{code}/excepcion?target=compara</summary>
    [HttpDelete("excepcion")]
    [Authorize]
    public async Task<IActionResult> Borrar(string code, [FromQuery] string? target, CancellationToken cancellationToken)
    {
        if (!TryParseTarget(target, out var t))
            return BadRequest(new { error = "target debe ser 'compras' o 'compara'" });
        try
        {
            bool ok = await _repository.BorrarExcepcionAsync(code, t, Contexto(), cancellationToken);
            return ok ? NoContent() : NotFound(new { error = "No había excepción para ese producto y destino" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error borrando la excepción de {Code}", code);
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

using ComprasParaguai.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ComprasParaguai.Api.Controllers;

/// <summary>
/// Pedidos de verificación a Worker.Verify. La API solo los registra: el contenedor worker-verify (--servicio)
/// los toma y los ejecuta, que es quien tiene el token de Compras. Pedir una verificación requiere sesión iniciada.
/// </summary>
[ApiController]
[Route("api/verify")]
public class VerifySolicitudesController : ControllerBase
{
    private readonly IVerifySolicitudRepository _repository;
    private readonly ILogger<VerifySolicitudesController> _logger;

    public VerifySolicitudesController(IVerifySolicitudRepository repository, ILogger<VerifySolicitudesController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>GET /api/verify/solicitudes?limit=10 — los pedidos más recientes (abiertos y cerrados).</summary>
    [HttpGet("solicitudes")]
    public async Task<ActionResult<List<VerifySolicitudDto>>> Listar([FromQuery] int limit = 10, CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _repository.ListarAsync(limit, cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cargando las solicitudes de verificación");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// POST /api/verify/solicitudes?tipo=disponibilidad|muestra — pide una verificación. Si ya hay una abierta del mismo
    /// tipo devuelve esa (<c>nueva = false</c>) en vez de crear otra.
    /// </summary>
    [HttpPost("solicitudes")]
    [Authorize]
    public async Task<ActionResult> Solicitar([FromQuery] string? tipo, CancellationToken cancellationToken = default)
    {
        if (!VerifyTipo.EsValido(tipo))
            return BadRequest(new { error = $"tipo inválido; use '{VerifyTipo.Disponibilidad}' o '{VerifyTipo.Muestra}'" });

        try
        {
            var (solicitud, nueva) = await _repository.SolicitarAsync(tipo!, "panel", User.Identity?.Name, cancellationToken);
            return Accepted(new { solicitud, nueva });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registrando la solicitud de verificación");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

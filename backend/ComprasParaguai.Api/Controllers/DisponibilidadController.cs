using ComprasParaguai.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ComprasParaguai.Api.Controllers;

/// <summary>
/// Productos con stock que Compras Paraguai marca como no disponibles. Lee la foto que toma
/// <c>Worker.Verify --disponibilidad</c>; la API nunca llama a Compras ni conoce su token.
/// </summary>
[ApiController]
[Route("api/compras")]
public class DisponibilidadController : ControllerBase
{
    private readonly IDisponibilidadRepository _repository;
    private readonly ILogger<DisponibilidadController> _logger;

    public DisponibilidadController(IDisponibilidadRepository repository, ILogger<DisponibilidadController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>GET /api/compras/no-disponibles?page=1&amp;pageSize=50</summary>
    [HttpGet("no-disponibles")]
    public async Task<ActionResult<NoDisponiblesDto>> GetNoDisponibles(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _repository.ListarAsync(page, pageSize, cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cargando los productos no disponibles en Compras");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>GET /api/compras/no-disponibles/codigos: solo los códigos, para marcar filas en Productos.</summary>
    [HttpGet("no-disponibles/codigos")]
    public async Task<ActionResult<List<string>>> GetCodigos(CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _repository.CodigosAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cargando los códigos no disponibles en Compras");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

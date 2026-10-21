using ComprasParaguai.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ComprasParaguai.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CambiosController : ControllerBase
{
    private readonly IHistorialRepository _repository;
    private readonly ILogger<CambiosController> _logger;

    public CambiosController(IHistorialRepository repository, ILogger<CambiosController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Últimos cambios de productos, con diff por campo. Todos los filtros son opcionales.
    /// GET /api/cambios?code=&amp;q=&amp;marcaId=&amp;categoriaId=&amp;campo=price&amp;origen=sync&amp;desde=2026-10-01&amp;hasta=2026-10-21&amp;page=1&amp;pageSize=50
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<CambioListDto>> GetAll(
        [FromQuery] string? code = null,
        [FromQuery] string? q = null,
        [FromQuery] int? marcaId = null,
        [FromQuery] int? categoriaId = null,
        [FromQuery] string? campo = null,
        [FromQuery] string? origen = null,
        [FromQuery] DateOnly? desde = null,
        [FromQuery] DateOnly? hasta = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _repository.BuscarCambiosAsync(new CambiosFiltro
            {
                Code = code,
                Texto = q,
                MarcaId = marcaId,
                CategoriaId = categoriaId,
                Campo = campo,
                Origen = origen,
                Desde = desde,
                Hasta = hasta,
                Page = page,
                PageSize = pageSize
            }, cancellationToken);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cargando cambios");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

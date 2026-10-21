using ComprasParaguai.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ComprasParaguai.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MarcasController : ControllerBase
{
    private readonly ICatalogoRepository _repository;
    private readonly IEnvioReglaRepository _reglas;
    private readonly ILogger<MarcasController> _logger;

    public MarcasController(ICatalogoRepository repository, IEnvioReglaRepository reglas, ILogger<MarcasController> logger)
    {
        _repository = repository;
        _reglas = reglas;
        _logger = logger;
    }

    /// <summary>
    /// Departamentos y secciones donde la marca tiene productos (para pausar "marca en categoría").
    /// GET /api/marcas/{id}/categorias?target=compras
    /// </summary>
    [HttpGet("{id:int}/categorias")]
    public async Task<ActionResult<List<MarcaCategoriaDto>>> GetCategorias(int id, [FromQuery] string? target, CancellationToken cancellationToken)
    {
        var t = target?.ToLowerInvariant() is "compara" or "comparaparaguai"
            ? ComprasParaguai.Core.Models.SyncTarget.ComparaParaguai
            : ComprasParaguai.Core.Models.SyncTarget.ComprasParaguai;
        try
        {
            return Ok(await _reglas.CategoriasDeMarcaAsync(id, t, cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cargando las categorías de la marca {Id}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Marcas con conteo de productos, ordenadas por cantidad.
    /// GET /api/marcas?search=adi&amp;soloActivas=true&amp;page=1&amp;pageSize=50
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<MarcaListDto>> GetAll(
        [FromQuery] string? search = null,
        [FromQuery] bool soloActivas = true,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _repository.GetMarcasAsync(search, soloActivas, page, pageSize, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cargando marcas");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

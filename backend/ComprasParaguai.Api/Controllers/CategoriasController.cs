using ComprasParaguai.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ComprasParaguai.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriasController : ControllerBase
{
    private readonly ICatalogoRepository _repository;
    private readonly ILogger<CategoriasController> _logger;

    public CategoriasController(ICatalogoRepository repository, ILogger<CategoriasController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Árbol de categorías con conteo de productos (incluye descendientes).
    /// GET /api/categorias/arbol?soloActivas=true
    /// </summary>
    [HttpGet("arbol")]
    public async Task<ActionResult<List<CategoriaNodoDto>>> GetArbol(
        [FromQuery] bool soloActivas = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var arbol = await _repository.GetArbolCategoriasAsync(soloActivas, cancellationToken);
            return Ok(arbol);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cargando el árbol de categorías");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

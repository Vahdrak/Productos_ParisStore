using ComprasParaguai.Core.Interfaces;
using ComprasParaguai.Core.Models;
using Microsoft.AspNetCore.Mvc;
using static ComprasParaguai.Core.Interfaces.IProductoRepository;

namespace ComprasParaguai.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductosController : ControllerBase
{
    private readonly IProductoRepository _repository;
    private readonly ILogger<ProductosController> _logger;

    public ProductosController(IProductoRepository repository, ILogger<ProductosController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Lista todos los productos Pendiente de envío.
    /// GET /api/productos/pending
    /// </summary>
    [HttpGet("pending")]
    public async Task<ActionResult<List<ProductoApi>>> GetPending(CancellationToken cancellationToken)
    {
        try
        {
            var products = await _repository.LoadPendingProductsAsync(cancellationToken);
            return Ok(products);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cargando productos pendientes");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Obtiene un producto por código.
    /// GET /api/productos/{code}
    /// </summary>
    [HttpGet("{code}")]
    public async Task<ActionResult<ProductoApi>> GetByCode(string code, CancellationToken cancellationToken)
    {
        try
        {
            var products = await _repository.LoadPendingProductsAsync(cancellationToken);
            var product = products.FirstOrDefault(p => p.Code == code);

            if (product == null)
                return NotFound(new { message = $"Producto {code} no encontrado" });

            return Ok(product);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cargando producto {Code}", code);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Obtiene el historial de cambios de un producto.
    /// GET /api/productos/{code}/historial
    /// </summary>
    [HttpGet("{code}/historial")]
    public async Task<ActionResult<List<ProductHistorialDto>>> GetHistorial(string code, CancellationToken cancellationToken)
    {
        try
        {
            var historial = await _repository.LoadProductHistorialAsync(code, cancellationToken);
            return Ok(historial);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cargando historial para {Code}", code);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Obtiene la comparación antes/después del producto.
    /// GET /api/productos/{code}/diff
    /// </summary>
    [HttpGet("{code}/diff")]
    public async Task<ActionResult<ProductDiffDto>> GetDiff(string code, CancellationToken cancellationToken)
    {
        try
        {
            var current = await _repository.LoadProductByCodeAsync(code, cancellationToken);
            if (current == null)
                return NotFound(new { message = $"Producto {code} no encontrado" });

            var lastHistorial = await _repository.GetLastHistorialAsync(code, cancellationToken);

            var diff = new ProductDiffDto
            {
                Code = code,
                Current = current,
                Previous = lastHistorial,
                HasChanges = lastHistorial != null
            };

            return Ok(diff);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generando diff para {Code}", code);
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

public class ProductDiffDto
{
    public string Code { get; set; }
    public ProductoApi Current { get; set; }
    public ProductHistorialDto? Previous { get; set; }
    public bool HasChanges { get; set; }
}

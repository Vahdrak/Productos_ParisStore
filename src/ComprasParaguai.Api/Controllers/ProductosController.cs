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
    /// Lista productos agrupados por categoría con paginación por categoría.
    /// GET /api/productos/categories?pageSize=15
    /// </summary>
    [HttpGet("categories")]
    public async Task<ActionResult<List<CategoryProductsDto>>> GetByCategories(
        [FromQuery] int pageSize = 15,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var products = await _repository.LoadPendingProductsAsync(cancellationToken);

            var grouped = products
                .GroupBy(p => p.Brand ?? "Sin Categoría")
                .OrderBy(g => g.Key)
                .Select(g => new CategoryProductsDto
                {
                    Category = g.Key,
                    TotalCount = g.Count(),
                    Products = g.Take(pageSize).ToList(),
                    PageSize = pageSize
                })
                .ToList();

            return Ok(grouped);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cargando productos por categoría");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Lista todos los productos con filtros opcionales.
    /// GET /api/productos?syncStatus=0&page=1&pageSize=50
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ProductListDto>> GetAll(
        [FromQuery] int? syncStatus = null,
        [FromQuery] string? code = null,
        [FromQuery] string? name = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var pending = await _repository.LoadPendingProductsAsync(cancellationToken);

            var filtered = pending.AsEnumerable();
            if (syncStatus.HasValue)
                filtered = filtered.Where(p => p.SyncStatus == syncStatus.Value);
            if (!string.IsNullOrEmpty(code))
                filtered = filtered.Where(p => p.Code.Contains(code, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(name))
                filtered = filtered.Where(p => p.NamePt?.Contains(name, StringComparison.OrdinalIgnoreCase) == true);

            var total = filtered.Count();
            var items = filtered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Ok(new ProductListDto
            {
                Items = items,
                TotalCount = total,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cargando productos");
            return StatusCode(500, new { error = ex.Message });
        }
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

public class ProductListDto
{
    public List<ProductoApi> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class CategoryProductsDto
{
    public string Category { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public List<ProductoApi> Products { get; set; } = [];
    public int PageSize { get; set; }
}

using ComprasParaguai.Core.Interfaces;
using ComprasParaguai.Core.Models;
using ComprasParaguai.Core.Services;
using Microsoft.AspNetCore.Mvc;
using static ComprasParaguai.Core.Interfaces.IProductoRepository;

namespace ComprasParaguai.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductosController : ControllerBase
{
    private readonly IProductoRepository _repository;
    private readonly ImageUrlService _imageUrlService;
    private readonly ILogger<ProductosController> _logger;

    public ProductosController(IProductoRepository repository, ImageUrlService imageUrlService, ILogger<ProductosController> logger)
    {
        _repository = repository;
        _imageUrlService = imageUrlService;
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

            // Procesar URLs de imágenes
            var processed = products.Select(p =>
            {
                p.UrlImage = _imageUrlService.BuildImageUrl(p.UrlImage);
                return p;
            }).ToList();

            var grouped = processed
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
            var result = await _repository.SearchProductsAsync(code, name, syncStatus, page, pageSize, cancellationToken);
            return Ok(result);
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

    /// <summary>
    /// Actualiza un producto (para pruebas).
    /// PUT /api/productos/{code}
    /// </summary>
    [HttpPut("{code}")]
    public async Task<ActionResult<ProductoApi>> UpdateProduct(string code, [FromBody] ProductoApi updatedProduct, CancellationToken cancellationToken)
    {
        try
        {
            var current = await _repository.LoadProductByCodeAsync(code, cancellationToken);
            if (current == null)
                return NotFound(new { message = $"Producto {code} no encontrado" });

            current.NamePt = updatedProduct.NamePt;
            current.Price = updatedProduct.Price;
            current.Stock = updatedProduct.Stock;
            current.DescriptionPt = updatedProduct.DescriptionPt;
            if (updatedProduct.Brand != null) current.Brand = updatedProduct.Brand;
            if (updatedProduct.UrlImage != null) current.UrlImage = updatedProduct.UrlImage;

            var payload = new
            {
                current.Code,
                current.NameEs,
                current.NamePt,
                current.Price,
                current.PriceIva,
                current.Stock,
                current.Brand,
                current.DescriptionEs,
                current.DescriptionPt,
                current.UrlImage,
                current.Link,
                current.LinkPurchase,
                current.ForceImageUpdate
            };
            string json = System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions { WriteIndented = false });
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(bytes);
                current.SourceHash = Convert.ToHexString(hashBytes).ToLower();
            }

            await _repository.UpsertProductsAsync(new List<ProductoApi> { current }, DateTime.UtcNow, cancellationToken);
            return Ok(current);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando producto {Code}", code);
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

public class ProductDiffDto
{
    public string Code { get; set; } = string.Empty;
    public ProductoApi Current { get; set; } = default!;
    public ProductHistorialDto? Previous { get; set; }
    public bool HasChanges { get; set; }
}



public class CategoryProductsDto
{
    public string Category { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public List<ProductoApi> Products { get; set; } = [];
    public int PageSize { get; set; }
}

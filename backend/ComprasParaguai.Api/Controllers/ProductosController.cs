using ComprasParaguai.Core.Interfaces;
using ComprasParaguai.Core.Models;
using Microsoft.AspNetCore.Authorization;
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
    /// Lista todos los productos con filtros opcionales.
    /// GET /api/productos?syncStatus=0&target=compras&marcaId=12&categoriaId=34&page=1&pageSize=50
    /// (categoriaId incluye todo el subárbol de esa categoría)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ProductListDto>> GetAll(
        [FromQuery] int? syncStatus = null,
        [FromQuery] string? code = null,
        [FromQuery] string? name = null,
        [FromQuery] string? target = null,
        [FromQuery] int? marcaId = null,
        [FromQuery] int? categoriaId = null,
        [FromQuery] bool? regimen = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var syncTarget = ParseTarget(target);
            var result = await _repository.SearchProductsAsync(
                code, name, syncStatus, page, pageSize, syncTarget, marcaId, categoriaId, regimen, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cargando productos");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    public record ReenviarBody(
        string? Target, string[]? Codes, string? Code, string? Name, int? SyncStatus, string? FiltroTarget,
        int? MarcaId, int? CategoriaId, bool? Regimen, bool Todos = false);

    /// <summary>
    /// Marca para reenviar: vuelve a Pendiente los productos Enviados o en Error del destino ("compras", "compara" o "ambos").
    /// Se indican códigos, o el filtro de la lista (code, name, syncStatus + filtroTarget, marcaId, categoriaId, regimen);
    /// sin códigos ni filtro hay que pedir <c>todos: true</c>. Los retenidos por una pausa quedan Pendientes pero no se envían.
    /// POST /api/productos/reenviar
    /// </summary>
    [HttpPost("reenviar")]
    [Authorize]
    public async Task<IActionResult> Reenviar([FromBody] ReenviarBody body, CancellationToken cancellationToken)
    {
        var destinos = new List<SyncTarget>();
        switch (body.Target?.ToLowerInvariant())
        {
            case "compras": destinos.Add(SyncTarget.ComprasParaguai); break;
            case "compara": destinos.Add(SyncTarget.ComparaParaguai); break;
            case "ambos": destinos.Add(SyncTarget.ComprasParaguai); destinos.Add(SyncTarget.ComparaParaguai); break;
            default: return BadRequest(new { error = "target debe ser 'compras', 'compara' o 'ambos'" });
        }

        var filtro = new ProductosFiltro
        {
            Code = body.Code, Name = body.Name, SyncStatus = body.SyncStatus, Target = ParseTarget(body.FiltroTarget),
            MarcaId = body.MarcaId, CategoriaId = body.CategoriaId, Regimen = body.Regimen
        };
        bool hayCodigos = body.Codes is { Length: > 0 };
        if (!hayCodigos && filtro.SinFiltros && !body.Todos)
            return BadRequest(new { error = "Indica códigos, un filtro, o todos=true para reenviar todo" });

        try
        {
            int n = await _repository.ReenviarAsync(destinos, filtro, hayCodigos ? body.Codes : null, cancellationToken);
            _logger.LogInformation("{Usuario} marcó {N} productos para reenviar a {Destinos}", User.Identity?.Name ?? "?", n, body.Target);
            return Ok(new { reiniciados = n });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marcando productos para reenviar");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Pendientes de envío paginados, indicando cuáles están retenidos por una pausa o excepción.
    /// GET /api/productos/pending?target=compras&amp;page=1&amp;pageSize=50&amp;code=&amp;regimen=true&amp;retencion=retenidos
    /// </summary>
    [HttpGet("pending")]
    public async Task<ActionResult<ProductoPendienteListDto>> GetPending(
        [FromQuery] string? target = null,
        [FromQuery] string? code = null,
        [FromQuery] string? name = null,
        [FromQuery] int? marcaId = null,
        [FromQuery] int? categoriaId = null,
        [FromQuery] bool? regimen = null,
        [FromQuery] string? retencion = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var syncTarget = ParseTarget(target);
            var result = await _repository.BuscarPendientesAsync(syncTarget, new PendientesFiltro
            {
                Code = code,
                Name = name,
                MarcaId = marcaId,
                CategoriaId = categoriaId,
                Regimen = regimen,
                Retencion = retencion,
                Page = page,
                PageSize = pageSize
            }, cancellationToken);
            return Ok(result);
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
            var product = await _repository.LoadProductByCodeAsync(code, cancellationToken);

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
    [Authorize]
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

            // Queda en el historial con origen "api" y, cuando haya login, el usuario.
            var contexto = new CambioContexto(OrigenCambio.Api, User.Identity?.Name);
            await _repository.UpsertProductsAsync(new List<ProductoApi> { current }, DateTime.UtcNow, contexto, cancellationToken);
            return Ok(current);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando producto {Code}", code);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Obtiene los cambios recientes de todos los productos con paginación.
    /// GET /api/productos/cambios/recientes?page=1&pageSize=20
    /// </summary>
    [HttpGet("cambios/recientes")]
    public async Task<ActionResult<dynamic>> GetRecentChanges(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _repository.GetRecentChangesAsync(page, pageSize, cancellationToken);
            var dto = new
            {
                items = result.Items.Select(c => new ChangeLogDto
                {
                    Code = c.Code,
                    ProductName = c.ProductName,
                    Field = c.Field,
                    Before = c.Before ?? "—",
                    After = c.After ?? "—",
                    ChangedAt = c.ChangedAt
                }).ToList(),
                totalCount = result.TotalCount,
                page = result.Page,
                pageSize = result.PageSize,
                totalPages = result.TotalPages
            };

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cargando cambios recientes");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Obtiene productos con errores.
    /// GET /api/productos/errors?target=compras
    /// </summary>
    [HttpGet("errors")]
    public async Task<ActionResult<List<ProductoWithErrorDto>>> GetErrors(
        [FromQuery] string? target = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var syncTarget = ParseTarget(target);
            var errors = await _repository.LoadErrorProductsAsync(syncTarget, cancellationToken);
            return Ok(errors);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cargando productos con error");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    private static SyncTarget ParseTarget(string? target) =>
        target?.ToLowerInvariant() switch
        {
            "compara" or "comparaparaguai" => SyncTarget.ComparaParaguai,
            _ => SyncTarget.ComprasParaguai
        };
}

public class ProductDiffDto
{
    public string Code { get; set; } = string.Empty;
    public ProductoApi Current { get; set; } = default!;
    public ProductHistorialDto? Previous { get; set; }
    public bool HasChanges { get; set; }
}




public class ChangeLogDto
{
    public string Code { get; set; } = string.Empty;
    public string? ProductName { get; set; }
    public string Field { get; set; } = string.Empty;
    public string Before { get; set; } = string.Empty;
    public string After { get; set; } = string.Empty;
    public DateTime? ChangedAt { get; set; }
}

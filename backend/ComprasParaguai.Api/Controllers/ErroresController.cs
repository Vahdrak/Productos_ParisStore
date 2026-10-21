using ComprasParaguai.Core.Interfaces;
using ComprasParaguai.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ComprasParaguai.Api.Controllers;

/// <summary>
/// Productos en Error por destino, agrupados por tipo de error, marca y departamento.
/// Ver es libre; reintentar (volver a Pendiente) exige sesión.
/// </summary>
[ApiController]
[Route("api/errores")]
public class ErroresController : ControllerBase
{
    private readonly IErrorEnvioRepository _repository;
    private readonly ILogger<ErroresController> _logger;

    public ErroresController(IErrorEnvioRepository repository, ILogger<ErroresController> logger)
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

    /// <summary>
    /// GET /api/errores?target=compras&amp;tipo=duplicado&amp;marcaId=&amp;categoriaId=&amp;q=&amp;page=1&amp;pageSize=50
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ErroresResultadoDto>> Get(
        [FromQuery] string? target, [FromQuery] string? tipo, [FromQuery] int? marcaId, [FromQuery] int? categoriaId,
        [FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        if (!TryParseTarget(target, out var t))
            return BadRequest(new { error = "target debe ser 'compras' o 'compara'" });
        try
        {
            var result = await _repository.BuscarAsync(t, new ErroresFiltro
            {
                Tipo = tipo, MarcaId = marcaId, CategoriaId = categoriaId, Texto = q, Page = page, PageSize = pageSize
            }, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cargando los errores de envío");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    public record ReintentarBody(string? Target, string? Tipo, int? MarcaId, int? CategoriaId, string? Q, string[]? Codes, bool Todos = false);

    /// <summary>
    /// Vuelve a Pendiente los productos en Error: los códigos indicados, o todos los que cumplan el filtro.
    /// Sin códigos ni filtro hay que pedir explícitamente <c>todos: true</c>. POST /api/errores/reintentar
    /// </summary>
    [HttpPost("reintentar")]
    [Authorize]
    public async Task<IActionResult> Reintentar([FromBody] ReintentarBody body, CancellationToken cancellationToken)
    {
        if (!TryParseTarget(body.Target, out var t))
            return BadRequest(new { error = "target debe ser 'compras' o 'compara'" });

        var filtro = new ErroresFiltro { Tipo = body.Tipo, MarcaId = body.MarcaId, CategoriaId = body.CategoriaId, Texto = body.Q };
        bool hayCodigos = body.Codes is { Length: > 0 };
        if (!hayCodigos && filtro.SinFiltros && !body.Todos)
            return BadRequest(new { error = "Indica códigos, un filtro, o todos=true para reintentar todos los errores" });

        try
        {
            int n = await _repository.ReintentarAsync(t, filtro, hayCodigos ? body.Codes : null, cancellationToken);
            _logger.LogInformation("{Usuario} reinició {N} productos en Error de {Target}", User.Identity?.Name ?? "?", n, t);
            return Ok(new { reiniciados = n });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reintentando productos");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

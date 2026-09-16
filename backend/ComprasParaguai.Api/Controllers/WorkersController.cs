using ComprasParaguai.Core.Interfaces;
using ComprasParaguai.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace ComprasParaguai.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WorkersController : ControllerBase
{
    private readonly ISendRunRepository _sendRunRepository;
    private readonly IVerifyRunRepository _verifyRunRepository;
    private readonly ILogger<WorkersController> _logger;

    public WorkersController(
        ISendRunRepository sendRunRepository,
        IVerifyRunRepository verifyRunRepository,
        ILogger<WorkersController> logger)
    {
        _sendRunRepository = sendRunRepository;
        _verifyRunRepository = verifyRunRepository;
        _logger = logger;
    }

    /// <summary>
    /// Obtiene el historial de corridas de envío para un destino.
    /// GET /api/workers/send-runs?target=compras&limit=50
    /// </summary>
    [HttpGet("send-runs")]
    public async Task<ActionResult<List<SendRunDto>>> GetSendRuns(
        [FromQuery] string? target = null,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var syncTarget = ParseTarget(target);
            var runs = await _sendRunRepository.GetRunsAsync(syncTarget, limit, cancellationToken);
            return Ok(runs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cargando send-runs");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Obtiene el historial de corridas de verificación.
    /// GET /api/workers/verify-runs?limit=20
    /// </summary>
    [HttpGet("verify-runs")]
    public async Task<ActionResult<List<VerifyRunDto>>> GetVerifyRuns(
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var runs = await _verifyRunRepository.GetRunsAsync(limit, cancellationToken);
            return Ok(runs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cargando verify-runs");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Obtiene los resultados de una corrida de verificación específica.
    /// GET /api/workers/verify-runs/{id}
    /// </summary>
    [HttpGet("verify-runs/{id}")]
    public async Task<ActionResult<List<VerifyResultDto>>> GetVerifyResults(
        long id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var results = await _verifyRunRepository.GetResultsAsync(id, cancellationToken);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cargando resultados de verificación para run {Id}", id);
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

using ComprasParaguai.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ComprasParaguai.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SyncRunsController : ControllerBase
{
    private readonly ISyncRunRepository _repository;
    private readonly ILogger<SyncRunsController> _logger;

    public SyncRunsController(ISyncRunRepository repository, ILogger<SyncRunsController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Lista últimas corridas de sync.
    /// GET /api/sync-runs?limit=10
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<SyncRunDto>>> GetRecent([FromQuery] int limit = 10, CancellationToken cancellationToken = default)
    {
        try
        {
            var lastRun = await _repository.GetLastSyncRunAsync(cancellationToken);
            if (lastRun == null)
                return Ok(new List<SyncRunDto>());

            var runs = new List<SyncRunDto> { lastRun };
            return Ok(runs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo corridas");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Retorna fecha de la última corrida exitosa.
    /// GET /api/sync-runs/last-successful
    /// </summary>
    [HttpGet("last-successful")]
    public async Task<ActionResult<LastSyncDto>> GetLastSuccessful(CancellationToken cancellationToken)
    {
        try
        {
            var date = await _repository.GetLastSuccessfulSyncAsync(cancellationToken);
            return Ok(new LastSyncDto { LastSyncAt = date });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo última corrida");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

public class LastSyncDto
{
    public DateTime? LastSyncAt { get; set; }
}

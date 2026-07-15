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
    /// Retorna fecha de la última corrida exitosa.
    /// GET /api/syncruns/last-successful
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

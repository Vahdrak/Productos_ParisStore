using ComprasParaguai.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using static ComprasParaguai.Core.Interfaces.ISyncRunRepository;

namespace ComprasParaguai.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly IProductoRepository _productoRepository;
    private readonly ISyncRunRepository _syncRunRepository;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(
        IProductoRepository productoRepository,
        ISyncRunRepository syncRunRepository,
        ILogger<DashboardController> logger)
    {
        _productoRepository = productoRepository;
        _syncRunRepository = syncRunRepository;
        _logger = logger;
    }

    /// <summary>
    /// Retorna contadores y resumen para el dashboard.
    /// GET /api/dashboard
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<DashboardDto>> GetDashboard(CancellationToken cancellationToken)
    {
        try
        {
            var pending = await _productoRepository.LoadPendingProductsAsync(cancellationToken);
            var lastSync = await _syncRunRepository.GetLastSuccessfulSyncAsync(cancellationToken);
            var lastSyncRun = await _syncRunRepository.GetLastSyncRunAsync(cancellationToken);

            var dto = new DashboardDto
            {
                PendingCount = pending.Count,
                LastSyncAt = lastSync,
                Status = pending.Count == 0 ? "OK" : "PENDING",
                NewCount = lastSyncRun?.NewCount ?? 0,
                UpdatedCount = lastSyncRun?.UpdatedCount ?? 0,
                SentCount = lastSyncRun?.SentCount ?? 0,
                ErrorCount = lastSyncRun?.ErrorCount ?? 0
            };

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en dashboard");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

public class DashboardDto
{
    public int PendingCount { get; set; }
    public int NewCount { get; set; }
    public int UpdatedCount { get; set; }
    public int SentCount { get; set; }
    public int ErrorCount { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public string Status { get; set; } = "UNKNOWN";
}

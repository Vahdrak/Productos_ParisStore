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
    public Task<ActionResult<DashboardDto>> GetDashboard(CancellationToken cancellationToken)
    {
        try
        {
            var dto = new DashboardDto
            {
                PendingCount = 1547,
                NewCount = 312,
                UpdatedCount = 156,
                SentCount = 45678,
                ErrorCount = 3,
                Status = "OK",
                LastSyncAt = DateTime.UtcNow.AddHours(-2)
            };
            return Task.FromResult<ActionResult<DashboardDto>>(Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en dashboard");
            return Task.FromResult<ActionResult<DashboardDto>>(
                StatusCode(500, new { error = ex.Message })
            );
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

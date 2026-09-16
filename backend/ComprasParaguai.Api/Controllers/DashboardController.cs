using ComprasParaguai.Core.Models;
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
    /// Retorna contadores y resumen para el dashboard con información de ambos destinos.
    /// GET /api/dashboard
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<DashboardDto>> GetDashboard(CancellationToken cancellationToken)
    {
        try
        {
            var statusCounts = await _productoRepository.GetStatusCountsAsync(cancellationToken);
            var lastSync = await _syncRunRepository.GetLastSuccessfulSyncAsync(cancellationToken);
            var lastSyncRun = await _syncRunRepository.GetLastSyncRunAsync(cancellationToken);

            var targets = statusCounts.Select(sc => new TargetStatusDto
            {
                Target = sc.Target.ToString(),
                Pending = sc.Pending,
                Sent = sc.Sent,
                Error = sc.Error,
                Total = sc.Pending + sc.Sent + sc.Error,
                LastSentAt = sc.LastSentAt ?? DateTime.MinValue
            }).ToList();

            var comprasPending = statusCounts.FirstOrDefault(sc => sc.Target == SyncTarget.ComprasParaguai)?.Pending ?? 0;

            var dto = new DashboardDto
            {
                Targets = targets,
                PendingCount = comprasPending,
                LastSyncAt = lastSync,
                Status = comprasPending == 0 ? "OK" : "PENDIENTE",
                LastSyncRun = lastSyncRun is not null ? new LastSyncRunDto
                {
                    StartedAt = lastSyncRun.StartedAt,
                    RowsInView = lastSyncRun.RowsInView,
                    NewCount = lastSyncRun.NewCount,
                    UpdatedCount = lastSyncRun.UpdatedCount,
                    Notes = lastSyncRun.Notes
                } : null
            };

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Database unavailable, returning empty dashboard");
            return Ok(new DashboardDto
            {
                Targets = new(),
                PendingCount = 0,
                Status = "NO_DATA",
                LastSyncAt = null
            });
        }
    }

    /// <summary>
    /// Obtiene el historial de todas las corridas de sincronización.
    /// GET /api/sync-runs
    /// </summary>
    [HttpGet("sync-runs")]
    public async Task<ActionResult<List<SyncRunDto>>> GetSyncRuns(CancellationToken cancellationToken)
    {
        try
        {
            var runs = await _syncRunRepository.GetAllSyncRunsAsync(50, cancellationToken);
            return Ok(runs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching sync runs");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Obtiene datos de tendencia de los últimos ciclos para visualización.
    /// GET /api/dashboard/trends?hours=24
    /// </summary>
    [HttpGet("trends")]
    public async Task<ActionResult<SyncTrendsDto>> GetTrends(
        [FromQuery] int hours = 24,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var runs = await _syncRunRepository.GetAllSyncRunsAsync(200, cancellationToken);

            // Filtrar por horas
            var cutoffTime = DateTime.UtcNow.AddHours(-hours);
            var filteredRuns = runs
                .Where(r => r.FinishedAt.HasValue && r.FinishedAt >= cutoffTime)
                .OrderBy(r => r.FinishedAt)
                .ToList();

            var trends = new SyncTrendsDto
            {
                Hours = hours,
                TotalCycles = filteredRuns.Count,
                Cycles = filteredRuns.Select(r => new CycleSummary
                {
                    Timestamp = r.FinishedAt,
                    RowsRead = r.RowsInView ?? 0,
                    NewCount = r.NewCount,
                    UpdatedCount = r.UpdatedCount,
                    SentCount = r.SentCount,
                    ErrorCount = r.ErrorCount,
                    Notes = r.Notes
                }).ToList(),
                Statistics = new TrendStatistics
                {
                    TotalNew = filteredRuns.Sum(r => r.NewCount),
                    TotalUpdated = filteredRuns.Sum(r => r.UpdatedCount),
                    TotalSent = filteredRuns.Sum(r => r.SentCount),
                    TotalErrors = filteredRuns.Sum(r => r.ErrorCount),
                    AverageRowsPerCycle = filteredRuns.Count > 0
                        ? (decimal)filteredRuns.Average(r => r.RowsInView ?? 0)
                        : 0,
                    SuccessRate = filteredRuns.Count > 0
                        ? (filteredRuns.Count(r => r.ErrorCount == 0) * 100m / filteredRuns.Count)
                        : 100
                }
            };

            return Ok(trends);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching trends");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

public class DashboardDto
{
    public List<TargetStatusDto> Targets { get; set; } = new();
    public int PendingCount { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public LastSyncRunDto? LastSyncRun { get; set; }
    public string Status { get; set; } = "UNKNOWN";
}

public class TargetStatusDto
{
    public string Target { get; set; } = string.Empty;
    public int Pending { get; set; }
    public int Sent { get; set; }
    public int Error { get; set; }
    public int Total { get; set; }
    public DateTime LastSentAt { get; set; }
}

public class LastSyncRunDto
{
    public DateTime? StartedAt { get; set; }
    public int? RowsInView { get; set; }
    public int NewCount { get; set; }
    public int UpdatedCount { get; set; }
    public string? Notes { get; set; }
}

public class SyncTrendsDto
{
    public int Hours { get; set; }
    public int TotalCycles { get; set; }
    public List<CycleSummary> Cycles { get; set; } = new();
    public TrendStatistics Statistics { get; set; } = new();
}

public class CycleSummary
{
    public DateTime? Timestamp { get; set; }
    public int RowsRead { get; set; }
    public int NewCount { get; set; }
    public int UpdatedCount { get; set; }
    public int SentCount { get; set; }
    public int ErrorCount { get; set; }
    public string? Notes { get; set; }
}

public class TrendStatistics
{
    public int TotalNew { get; set; }
    public int TotalUpdated { get; set; }
    public int TotalSent { get; set; }
    public int TotalErrors { get; set; }
    public decimal AverageRowsPerCycle { get; set; }
    public decimal SuccessRate { get; set; }
}

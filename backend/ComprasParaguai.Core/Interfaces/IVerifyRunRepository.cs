using ComprasParaguai.Core.Models;
using ComprasParaguai.Core.Services;

namespace ComprasParaguai.Core.Interfaces;

/// <summary>Bitácora de Worker.Verify: cada corrida y el detalle por producto.</summary>
public interface IVerifyRunRepository
{
    Task<long> StartAsync(SyncTarget target, int sampleSize, CancellationToken cancellationToken = default);
    Task FinishAsync(long runId, IReadOnlyList<VerifyResult> results, string? csvPath, CancellationToken cancellationToken = default);
    Task<List<VerifyRunDto>> GetRunsAsync(int limit = 20, CancellationToken cancellationToken = default);
    Task<List<VerifyResultDto>> GetResultsAsync(long runId, CancellationToken cancellationToken = default);
}

public class VerifyRunDto
{
    public long Id { get; set; }
    public string Target { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public int SampleSize { get; set; }
    public int Found { get; set; }
    public int PriceMatch { get; set; }
    public int StockMatch { get; set; }
    public string? CsvPath { get; set; }
}

public class VerifyResultDto
{
    public string Code { get; set; } = "";
    public string? NamePt { get; set; }
    public bool Found { get; set; }
    public decimal? PriceLocal { get; set; }
    public decimal? PriceApi { get; set; }
    public int? StockLocal { get; set; }
    public decimal? StockApi { get; set; }
    public bool? AvailableApi { get; set; }
    public DateTime? UpdatedAtApi { get; set; }
}

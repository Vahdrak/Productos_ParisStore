using ComprasParaguai.Core.Models;

namespace ComprasParaguai.Core.Interfaces;

/// <summary>Bitácora de los workers de envío: una fila por corrida (send-pending, send-all, test-batch, servicio).</summary>
public interface ISendRunRepository
{
    Task<long> StartAsync(SyncTarget target, string mode, CancellationToken cancellationToken = default);
    Task FinishAsync(long runId, SendResult result, string? error = null, CancellationToken cancellationToken = default);
    Task<List<SendRunDto>> GetRunsAsync(SyncTarget? target = null, int limit = 50, CancellationToken cancellationToken = default);
}

public class SendRunDto
{
    public long Id { get; set; }
    public string Target { get; set; } = "";
    public string Mode { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public int Total { get; set; }
    public int Sent { get; set; }
    public int Failed { get; set; }
    public string? Error { get; set; }
}

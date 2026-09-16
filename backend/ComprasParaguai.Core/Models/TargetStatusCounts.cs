namespace ComprasParaguai.Core.Models;

/// <summary>Cuántos productos hay en cada estado para un destino, y cuándo fue el último envío.</summary>
public record TargetStatusCounts(SyncTarget Target, int Pending, int Sent, int Error, DateTime? LastSentAt)
{
    public int Total => Pending + Sent + Error;
}

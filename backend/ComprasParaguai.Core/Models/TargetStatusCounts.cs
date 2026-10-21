namespace ComprasParaguai.Core.Models;

/// <summary>
/// Cuántos productos hay en cada estado para un destino, y cuándo fue el último envío.
/// <para><b>Pending</b> = pendientes que SÍ se enviarán; <b>Held</b> = pendientes retenidos por una pausa o excepción
/// (se actualizan en la BD pero no salen); <b>Retired</b> = productos a los que ya se les envió stock 0 al retirarlos.</para>
/// </summary>
public record TargetStatusCounts(SyncTarget Target, int Pending, int Sent, int Error, DateTime? LastSentAt, int Held = 0, int Retired = 0)
{
    public int Total => Pending + Held + Sent + Error;
}

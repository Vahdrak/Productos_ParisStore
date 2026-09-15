namespace ComprasParaguai.Core.Models;

/// <summary>
/// Parámetros de envío de un worker: destino, tamaño de lote, pausa entre lotes y
/// espera ante rate limit (HTTP 429) antes de reintentar el mismo lote.
/// </summary>
public record SendOptions(SyncTarget Target, int BatchSize, int DelayMs, int RateLimitWaitMs = 0, int RateLimitRetries = 0)
{
    public static SendOptions ComprasParaguai => new(SyncTarget.ComprasParaguai, BatchSize: 500, DelayMs: 0);

    // Compara Paraguai: 80 requests por hora = 1 cada 45 s. Con 46 s de pausa nunca se supera.
    // Si igual llega un 429 (otro proceso consumió cuota), esperar 5 min y reintentar hasta 3 veces.
    public static SendOptions ComparaParaguai => new(
        SyncTarget.ComparaParaguai,
        BatchSize: 1000,
        DelayMs: 46_000,
        RateLimitWaitMs: 5 * 60_000,
        RateLimitRetries: 3);
}

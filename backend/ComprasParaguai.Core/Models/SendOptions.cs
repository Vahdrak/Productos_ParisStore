namespace ComprasParaguai.Core.Models;

/// <summary>
/// Parámetros de envío de un worker: destino, tamaño de lote y pausa entre lotes.
/// </summary>
public record SendOptions(SyncTarget Target, int BatchSize, int DelayMs)
{
    public static SendOptions ComprasParaguai => new(SyncTarget.ComprasParaguai, BatchSize: 500, DelayMs: 0);

    // Compara Paraguai limita a 80 req/h; 1000 por lote + 900ms deja margen.
    public static SendOptions ComparaParaguai => new(SyncTarget.ComparaParaguai, BatchSize: 1000, DelayMs: 900);
}

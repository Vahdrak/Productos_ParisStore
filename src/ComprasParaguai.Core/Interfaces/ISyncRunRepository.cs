using ComprasParaguai.Core.Models;

namespace ComprasParaguai.Core.Interfaces;

/// <summary>
/// Repositorio de bitácora (sync_runs).
/// Registra cada corrida: inicio, fin, contadores, notas.
/// </summary>
public interface ISyncRunRepository
{
    /// <summary>
    /// Inicia una nueva corrida de sync. Retorna el ID para luego cerrarla.
    /// </summary>
    Task<long> StartSyncAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Cierra una corrida con resultados y estadísticas.
    /// </summary>
    Task FinishSyncAsync(
        long syncRunId,
        SyncResult syncResult,
        SendResult sendResult,
        string? notes = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene la última corrida exitosa (para saber cuándo fue la última reconciliación).
    /// </summary>
    Task<DateTime?> GetLastSuccessfulSyncAsync(CancellationToken cancellationToken = default);
}

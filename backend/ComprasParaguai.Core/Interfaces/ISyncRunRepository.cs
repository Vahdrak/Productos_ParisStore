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

    /// <summary>
    /// Obtiene la información completa de la última corrida de sync.
    /// </summary>
    Task<SyncRunDto?> GetLastSyncRunAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene el historial de todas las corridas de sync (últimas 50).
    /// </summary>
    Task<List<SyncRunDto>> GetAllSyncRunsAsync(int limit = 50, CancellationToken cancellationToken = default);
}

public class SyncRunDto
{
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public int? RowsInView { get; set; }
    public int NewCount { get; set; }
    public int UpdatedCount { get; set; }
    public int SentCount { get; set; }
    public int ErrorCount { get; set; }
    public string? Notes { get; set; }
}

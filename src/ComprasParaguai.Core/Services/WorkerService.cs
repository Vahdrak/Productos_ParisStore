using ComprasParaguai.Core.Interfaces;

namespace ComprasParaguai.Core.Services;

/// <summary>
/// Orquesta la sincronización Oracle → BD: sync + reconciliación + bitácora.
/// No envía a ninguna API; de eso se encargan los workers de envío.
/// </summary>
public class WorkerService : IWorkerService
{
    private readonly ISyncService _syncService;
    private readonly ISyncRunRepository _syncRunRepository;

    public WorkerService(ISyncService syncService, ISyncRunRepository syncRunRepository)
    {
        _syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
        _syncRunRepository = syncRunRepository ?? throw new ArgumentNullException(nameof(syncRunRepository));
    }

    /// <summary>
    /// Ejecuta ciclo de sync: read → map → compare → write → bitácora.
    /// </summary>
    public async Task ExecuteSyncAsync(CancellationToken cancellationToken = default)
    {
        long syncRunId = await _syncRunRepository.StartSyncAsync(cancellationToken);

        try
        {
            var syncResult = await _syncService.SyncAsync(cancellationToken);

            string notes = $"Ciclo: {syncResult.NewProducts} nuevos, " +
                          $"{syncResult.UpdatedProducts} actualizados, " +
                          $"{syncResult.UnchangedProducts} sin cambios, " +
                          $"{syncResult.InvalidProducts} inválidos.";

            await _syncRunRepository.FinishSyncAsync(
                syncRunId,
                syncResult,
                SendResult.Empty,
                notes,
                cancellationToken);
        }
        catch (Exception ex)
        {
            await _syncRunRepository.FinishSyncAsync(
                syncRunId,
                new SyncResult(0, 0, 0, 0, 0),
                SendResult.Empty,
                $"Error: {ex.Message}",
                cancellationToken);

            throw;
        }
    }

    /// <summary>
    /// Ejecuta reconciliación diaria: detecta bajas (productos no en vista).
    /// </summary>
    public async Task ExecuteDailyReconciliationAsync(CancellationToken cancellationToken = default)
    {
        await _syncService.ReconcileAsync(cancellationToken);
    }
}

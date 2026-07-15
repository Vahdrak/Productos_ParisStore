using ComprasParaguai.Core.Interfaces;

namespace ComprasParaguai.Core.Services;

/// <summary>
/// Orquesta la sincronización completa: sync + send + reconciliación.
/// Sin dependencias de logging (eso va en el Worker).
/// </summary>
public class WorkerService : IWorkerService
{
    private readonly ISyncService _syncService;
    private readonly ISendService _sendService;
    private readonly ISyncRunRepository _syncRunRepository;

    public WorkerService(
        ISyncService syncService,
        ISendService sendService,
        ISyncRunRepository syncRunRepository)
    {
        _syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
        _sendService = sendService ?? throw new ArgumentNullException(nameof(sendService));
        _syncRunRepository = syncRunRepository ?? throw new ArgumentNullException(nameof(syncRunRepository));
    }

    /// <summary>
    /// Ejecuta ciclo de sync: read → map → compare → write → send → bitácora.
    /// </summary>
    public async Task ExecuteSyncAsync(CancellationToken cancellationToken = default)
    {
        // Iniciar bitácora.
        long syncRunId = await _syncRunRepository.StartSyncAsync(cancellationToken);

        try
        {
            // 1. Sincronizar (leer vista, detectar cambios, guardar).
            var syncResult = await _syncService.SyncAsync(cancellationToken);

            // 2. Enviar productos Pendiente.
            var sendResult = await _sendService.SendPendingAsync(cancellationToken);

            // 3. Registrar en bitácora.
            string notes = $"Ciclo: {syncResult.NewProducts} nuevos, " +
                          $"{syncResult.UpdatedProducts} actualizados, " +
                          $"{syncResult.UnchangedProducts} sin cambios, " +
                          $"{syncResult.InvalidProducts} inválidos. " +
                          $"Enviados: {sendResult.Sent}/{sendResult.Total}.";

            await _syncRunRepository.FinishSyncAsync(
                syncRunId,
                syncResult,
                sendResult,
                notes,
                cancellationToken);
        }
        catch (Exception ex)
        {
            // Error: registrar en bitácora con mensaje.
            await _syncRunRepository.FinishSyncAsync(
                syncRunId,
                new SyncResult(0, 0, 0, 0, 0),
                new SendResult(0, 0, 0, new()),
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

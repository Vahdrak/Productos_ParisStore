namespace ComprasParaguai.Core.Interfaces;

/// <summary>
/// Servicio principal del Worker: orquesta sync + send + reconciliación.
/// Ejecutado cada 15 minutos (o según temporizador).
/// </summary>
public interface IWorkerService
{
    /// <summary>
    /// Ejecuta una corrida completa de sincronización:
    /// 1. Sync: leer vista → mapear → comparar → guardar en BD
    /// 2. Send: cargar Pendientes → batcar → enviar → actualizar BD
    /// 3. Registra bitácora en sync_runs
    /// </summary>
    Task ExecuteSyncAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Ejecuta reconciliación diaria (una vez al día):
    /// Detecta bajas (productos en BD que ya no están en vista).
    /// Se llama después de ExecuteSync, una sola vez por día.
    /// </summary>
    Task ExecuteDailyReconciliationAsync(CancellationToken cancellationToken = default);
}

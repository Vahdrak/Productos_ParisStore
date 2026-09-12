using ComprasParaguai.Core.Interfaces;
using Microsoft.Extensions.Hosting;

namespace Worker.Sync;

/// <summary>
/// Worker hosted service: ejecuta sync cada N minutos (Sync:IntervalMinutes, por defecto 120).
/// Ejecuta reconciliación diaria una vez al día.
/// </summary>
public class SyncWorker : IHostedService
{
    private readonly IWorkerService _workerService;
    private readonly TimeSpan _interval;
    private readonly ILogger<SyncWorker> _logger;
    private Timer? _timer;
    private DateTime? _lastReconciliationDate;

    public SyncWorker(
        IWorkerService workerService,
        TimeSpan interval,
        ILogger<SyncWorker> logger)
    {
        _workerService = workerService ?? throw new ArgumentNullException(nameof(workerService));
        _interval = interval;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Iniciando Worker. Intervalo: {IntervalMinutes} minutos", _interval.TotalMinutes);

        // Crear timer que ejecuta cada intervalo.
        _timer = new Timer(
            async state => await ExecuteSyncCycleAsync(),
            null,
            TimeSpan.Zero,  // Ejecutar inmediatamente al iniciar.
            _interval);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deteniendo Worker");
        _timer?.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Ejecuta un ciclo de sync.
    /// Si es la primera ejecución del día, también ejecuta reconciliación.
    /// </summary>
    private async Task ExecuteSyncCycleAsync()
    {
        try
        {
            _logger.LogInformation("=== Iniciando ciclo de sync ===");

            // Ejecutar sync.
            await _workerService.ExecuteSyncAsync();

            // Si no se ha ejecutado reconciliación hoy, ejecutarla.
            var today = DateTime.UtcNow.Date;
            if (_lastReconciliationDate?.Date != today)
            {
                _logger.LogInformation("Ejecutando reconciliación diaria");
                await _workerService.ExecuteDailyReconciliationAsync();
                _lastReconciliationDate = DateTime.UtcNow;
            }

            _logger.LogInformation("=== Ciclo completado ===");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ejecutando ciclo de sync");
            // No relanzar: dejar que siga intentando en el próximo intervalo.
        }
    }
}

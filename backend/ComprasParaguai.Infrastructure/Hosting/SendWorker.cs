using ComprasParaguai.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ComprasParaguai.Infrastructure.Hosting;

/// <summary>
/// Hosted service de envío: ejecuta SendPendingAsync al arrancar y luego cada intervalo.
/// Compartido por Worker.Compras y Worker.Compara (cada uno con su ISendService).
/// </summary>
public class SendWorker : IHostedService, IDisposable
{
    private readonly ISendService _sendService;
    private readonly TimeSpan _interval;
    private readonly string _targetName;
    private readonly ILogger<SendWorker> _logger;
    private Timer? _timer;
    private int _running;

    public SendWorker(ISendService sendService, TimeSpan interval, string targetName, ILogger<SendWorker> logger)
    {
        _sendService = sendService ?? throw new ArgumentNullException(nameof(sendService));
        _interval = interval;
        _targetName = targetName;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Iniciando envío a {Target}. Intervalo: {Hours} h", _targetName, _interval.TotalHours);
        _timer = new Timer(async _ => await RunAsync(), null, TimeSpan.Zero, _interval);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deteniendo envío a {Target}", _targetName);
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    private async Task RunAsync()
    {
        // Si un envío anterior sigue corriendo (lotes largos), no solapar.
        if (Interlocked.Exchange(ref _running, 1) == 1)
        {
            _logger.LogWarning("Envío a {Target} todavía en curso; se omite este tick", _targetName);
            return;
        }

        try
        {
            _logger.LogInformation("=== Enviando pendientes a {Target} ===", _targetName);
            var result = await _sendService.SendPendingAsync();
            _logger.LogInformation("=== {Target}: {Sent} enviados, {Failed} fallidos, {Total} total ===",
                _targetName, result.Sent, result.Failed, result.Total);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enviando a {Target}", _targetName);
        }
        finally
        {
            Interlocked.Exchange(ref _running, 0);
        }
    }

    public void Dispose() => _timer?.Dispose();
}

using ComprasParaguai.Core.Interfaces;
using ComprasParaguai.Core.Models;

namespace ComprasParaguai.Core.Services;

/// <summary>
/// Orquesta el envío de productos a un destino (SendOptions.Target).
/// Batcha, envía por el cliente, marca Enviado/Error en la columna de estado de ese destino
/// y registra la corrida en send_runs (si hay repositorio de corridas).
/// </summary>
public class SendService : ISendService
{
    private readonly IProductoRepository _repository;
    private readonly IComprasParaguaiClient _client;
    private readonly SendOptions _options;
    private readonly ISendRunRepository? _runs;

    public SendService(IProductoRepository repository, IComprasParaguaiClient client, SendOptions options, ISendRunRepository? runs = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _runs = runs;
    }

    public Task<SendResult> SendAllAsync(CancellationToken cancellationToken = default) =>
        RunAsync("all", ct => _repository.LoadAllProductsAsync(_options.Target, ct), cancellationToken);

    /// <summary>
    /// Ciclo normal de un destino, en este orden:
    /// 1) libera los productos retirados que ya no están retenidos (vuelven a Pendiente),
    /// 2) retira del destino (stock 0) lo que una pausa con "retirar" cubre y ya estaba publicado,
    /// 3) envía los pendientes enviables (sin los retenidos).
    /// </summary>
    public async Task<SendResult> SendPendingAsync(CancellationToken cancellationToken = default)
    {
        await _repository.LiberarRetiradosAsync(_options.Target, cancellationToken);
        await RetirarAsync(cancellationToken);
        return await RunAsync("pending", ct => _repository.LoadPendingProductsAsync(_options.Target, ct), cancellationToken);
    }

    public Task<SendResult> SendSampleAsync(int count, CancellationToken cancellationToken = default) =>
        RunAsync("sample", ct => _repository.LoadRandomProductsAsync(count, _options.Target, ct), cancellationToken);

    /// <summary>
    /// Envía stock 0 a los productos ya publicados que una pausa con "retirar del destino" cubre.
    /// No cambia su estado de envío: queda registrado como retirado y, al liberarse, se reenvían.
    /// </summary>
    private async Task RetirarAsync(CancellationToken cancellationToken)
    {
        var aRetirar = await _repository.LoadParaRetirarAsync(_options.Target, cancellationToken);
        if (aRetirar is null || aRetirar.Count == 0)
            return;

        long? runId = _runs is null ? null : await _runs.StartAsync(_options.Target, "retiro", cancellationToken);
        try
        {
            foreach (var producto in aRetirar)
                producto.Stock = 0;

            var result = await SendBatchesAsync(aRetirar, cancellationToken, retiro: true);
            if (runId is { } id)
                await _runs!.FinishAsync(id, result, null, cancellationToken);
        }
        catch (Exception ex)
        {
            if (runId is { } id)
                await _runs!.FinishAsync(id, SendResult.Empty, ex.Message, cancellationToken);
            throw;
        }
    }

    private async Task<SendResult> RunAsync(string mode, Func<CancellationToken, Task<List<ProductoApi>>> load, CancellationToken cancellationToken)
    {
        long? runId = _runs is null ? null : await _runs.StartAsync(_options.Target, mode, cancellationToken);
        try
        {
            var products = await load(cancellationToken);
            var result = await SendBatchesAsync(products, cancellationToken);
            if (runId is { } id)
                await _runs!.FinishAsync(id, result, null, cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            if (runId is { } id)
                await _runs!.FinishAsync(id, SendResult.Empty, ex.Message, cancellationToken);
            throw;
        }
    }

    private async Task<SendResult> SendBatchesAsync(List<ProductoApi> products, CancellationToken cancellationToken, bool retiro = false)
    {
        if (products.Count == 0)
            return SendResult.Empty;

        int sent = 0, failed = 0;
        var failedCodes = new List<string>();
        var batches = products.Chunk(_options.BatchSize).ToList();

        for (int i = 0; i < batches.Count; i++)
        {
            var batch = batches[i];
            try
            {
                var response = await _client.ImportProductsAsync(batch, cancellationToken);

                // 429: la cuota por hora se agotó. Esperar y reintentar el mismo lote en vez de marcarlo en Error.
                for (int attempt = 0; response.RateLimited && attempt < _options.RateLimitRetries; attempt++)
                {
                    await Task.Delay(_options.RateLimitWaitMs, cancellationToken);
                    response = await _client.ImportProductsAsync(batch, cancellationToken);
                }

                if (response.Success)
                {
                    // El lote se procesó, pero la API puede rechazar productos individuales (207 Multi-Status).
                    var rejected = response.FailedProducts();
                    foreach (var (code, error) in rejected)
                    {
                        await _repository.MarkAsErrorAsync(code, retiro ? $"Retiro: {error}" : error, _options.Target, cancellationToken);
                        failedCodes.Add(code);
                    }

                    var accepted = batch.Select(p => p.Code).Where(c => !rejected.ContainsKey(c)).ToList();
                    if (accepted.Count > 0 && retiro)
                        await _repository.MarkRetiradosAsync(accepted, _options.Target, cancellationToken);
                    else if (accepted.Count > 0)
                        await _repository.MarkAsSentAsync(accepted, DateTime.UtcNow, _options.Target, cancellationToken);

                    sent += accepted.Count;
                    failed += rejected.Count;
                }
                else
                {
                    string errorMsg = (retiro ? "Retiro: " : "") + (response.Message ?? "Error desconocido");
                    foreach (var product in batch)
                    {
                        await _repository.MarkAsErrorAsync(product.Code, errorMsg, _options.Target, cancellationToken);
                        failedCodes.Add(product.Code);
                    }
                    failed += batch.Length;
                }
            }
            catch (Exception ex)
            {
                foreach (var product in batch)
                {
                    await _repository.MarkAsErrorAsync(product.Code, retiro ? $"Retiro: {ex.Message}" : ex.Message, _options.Target, cancellationToken);
                    failedCodes.Add(product.Code);
                }
                failed += batch.Length;
            }

            if (_options.DelayMs > 0 && i < batches.Count - 1)
                await Task.Delay(_options.DelayMs, cancellationToken);
        }

        return new SendResult(sent, failed, products.Count, failedCodes);
    }
}

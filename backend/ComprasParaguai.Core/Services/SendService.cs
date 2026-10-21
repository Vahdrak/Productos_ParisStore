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

    /// <summary>
    /// Máximo de pedidos extra por lote al aislar un producto "venenoso". Con lotes de 1000 y un solo culpable
    /// hacen falta ~20; el tope evita una avalancha de pedidos si falla todo (p. ej. el servidor caído).
    /// </summary>
    private const int MaxPedidosAislamiento = 60;

    /// <summary>Contadores y estado compartidos mientras se procesa un lote (y sus sub-lotes).</summary>
    private sealed class Acumulado
    {
        public int Sent;
        public int Failed;
        public readonly List<string> FailedCodes = new();
        public int PedidosExtra;
    }

    private async Task<SendResult> SendBatchesAsync(List<ProductoApi> products, CancellationToken cancellationToken, bool retiro = false)
    {
        if (products.Count == 0)
            return SendResult.Empty;

        var acc = new Acumulado();
        var batches = products.Chunk(_options.BatchSize).ToList();

        for (int i = 0; i < batches.Count; i++)
        {
            acc.PedidosExtra = 0;
            await ProcesarLoteAsync(batches[i], retiro, acc, cancellationToken);

            if (_options.DelayMs > 0 && i < batches.Count - 1)
                await Task.Delay(_options.DelayMs, cancellationToken);
        }

        return new SendResult(acc.Sent, acc.Failed, products.Count, acc.FailedCodes);
    }

    /// <summary>Envía el lote (con reintento por 429).</summary>
    private async Task<ImportResponse> EnviarLoteAsync(ProductoApi[] batch, CancellationToken cancellationToken)
    {
        var response = await _client.ImportProductsAsync(batch, cancellationToken);

        // 429: la cuota por hora se agotó. Esperar y reintentar el mismo lote en vez de marcarlo en Error.
        for (int attempt = 0; response.RateLimited && attempt < _options.RateLimitRetries; attempt++)
        {
            await Task.Delay(_options.RateLimitWaitMs, cancellationToken);
            response = await _client.ImportProductsAsync(batch, cancellationToken);
        }

        return response;
    }

    private async Task ProcesarLoteAsync(ProductoApi[] batch, bool retiro, Acumulado acc, CancellationToken cancellationToken)
    {
        try
        {
            var response = await EnviarLoteAsync(batch, cancellationToken);

            if (response.Success)
            {
                // El lote se procesó, pero la API puede rechazar productos individuales (207 Multi-Status).
                var rejected = response.FailedProducts();
                foreach (var (code, error) in rejected)
                {
                    await RegistrarErrorAsync(code, error, retiro, cancellationToken);
                    acc.FailedCodes.Add(code);
                }

                var accepted = batch.Select(p => p.Code).Where(c => !rejected.ContainsKey(c)).ToList();
                if (accepted.Count > 0 && retiro)
                    await _repository.MarkRetiradosAsync(accepted, _options.Target, cancellationToken);
                else if (accepted.Count > 0)
                    await _repository.MarkAsSentAsync(accepted, DateTime.UtcNow, _options.Target, cancellationToken);

                acc.Sent += accepted.Count;
                acc.Failed += rejected.Count;
                return;
            }

            // Error de servidor (5xx) en TODO el lote: casi siempre es UN producto que el destino no puede procesar
            // (p. ej. un slug duplicado) y el resto es daño colateral. Se parte el lote en mitades para aislarlo y
            // que solo ese producto quede en Error. Los 4xx, los 429 y las caídas de red no se parten.
            if (batch.Length > 1 && response.StatusCode >= 500 && !response.RateLimited && acc.PedidosExtra < MaxPedidosAislamiento)
            {
                int mitad = batch.Length / 2;
                var izquierda = batch[..mitad];
                var derecha = batch[mitad..];

                foreach (var parte in new[] { izquierda, derecha })
                {
                    if (_options.DelayMs > 0)
                        await Task.Delay(_options.DelayMs, cancellationToken);
                    acc.PedidosExtra++;
                    await ProcesarLoteAsync(parte, retiro, acc, cancellationToken);
                }

                return;
            }

            string errorMsg = response.Message ?? "Error desconocido";
            await MarcarErrorAsync(batch, errorMsg, retiro, acc, cancellationToken);
        }
        catch (Exception ex)
        {
            await MarcarErrorAsync(batch, ex.Message, retiro, acc, cancellationToken);
        }
    }

    private Task RegistrarErrorAsync(string code, string mensaje, bool retiro, CancellationToken cancellationToken) =>
        retiro
            ? _repository.MarkRetiroErrorAsync(code, mensaje, _options.Target, cancellationToken)
            : _repository.MarkAsErrorAsync(code, mensaje, _options.Target, cancellationToken);

    private async Task MarcarErrorAsync(ProductoApi[] batch, string mensaje, bool retiro, Acumulado acc, CancellationToken cancellationToken)
    {
        foreach (var product in batch)
        {
            await RegistrarErrorAsync(product.Code, mensaje, retiro, cancellationToken);
            acc.FailedCodes.Add(product.Code);
        }
        acc.Failed += batch.Length;
    }
}

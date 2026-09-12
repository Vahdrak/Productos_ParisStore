using ComprasParaguai.Core.Interfaces;
using ComprasParaguai.Core.Models;

namespace ComprasParaguai.Core.Services;

/// <summary>
/// Orquesta el envío de productos a un destino (SendOptions.Target).
/// Batcha, envía por el cliente, y marca Enviado/Error en la columna de estado de ese destino.
/// </summary>
public class SendService : ISendService
{
    private readonly IProductoRepository _repository;
    private readonly IComprasParaguaiClient _client;
    private readonly SendOptions _options;

    public SendService(IProductoRepository repository, IComprasParaguaiClient client, SendOptions options)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<SendResult> SendAllAsync(CancellationToken cancellationToken = default)
    {
        var all = await _repository.LoadAllProductsAsync(cancellationToken);
        return await SendBatchesAsync(all, cancellationToken);
    }

    public async Task<SendResult> SendPendingAsync(CancellationToken cancellationToken = default)
    {
        var pending = await _repository.LoadPendingProductsAsync(_options.Target, cancellationToken);
        return await SendBatchesAsync(pending, cancellationToken);
    }

    public async Task<SendResult> SendSampleAsync(int count, CancellationToken cancellationToken = default)
    {
        var sample = await _repository.LoadRandomProductsAsync(count, cancellationToken);
        return await SendBatchesAsync(sample, cancellationToken);
    }

    private async Task<SendResult> SendBatchesAsync(List<ProductoApi> products, CancellationToken cancellationToken)
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

                if (response.Success)
                {
                    var codes = batch.Select(p => p.Code).ToList();
                    await _repository.MarkAsSentAsync(codes, DateTime.UtcNow, _options.Target, cancellationToken);
                    sent += batch.Length;
                }
                else
                {
                    foreach (var product in batch)
                    {
                        string errorMsg = response.Message ?? "Error desconocido";
                        var validationError = response.ValidationErrors.FirstOrDefault(e => e.Code == product.Code);
                        if (validationError != null)
                            errorMsg = $"{validationError.Field}: {validationError.Error}";

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
                    await _repository.MarkAsErrorAsync(product.Code, ex.Message, _options.Target, cancellationToken);
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

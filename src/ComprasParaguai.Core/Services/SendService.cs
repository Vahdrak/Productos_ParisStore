using ComprasParaguai.Core.Interfaces;
using ComprasParaguai.Core.Models;

namespace ComprasParaguai.Core.Services;

/// <summary>
/// Orquesta el envío de productos a Compras Paraguai.
/// Batca (500 por lote), reintenta, actualiza BD.
/// </summary>
public class SendService : ISendService
{
    private readonly IProductoRepository _repository;
    private readonly IComprasParaguaiClient _client;
    private const int BatchSize = 1000;

    public SendService(IProductoRepository repository, IComprasParaguaiClient client)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <summary>
    /// Carga TODOS los productos, los batca, envía cada lote, marca como Enviado en BD.
    /// Usado para envío full inicial a Compara Paraguai (delay 900ms para respetar rate limit).
    /// </summary>
    public async Task<SendResult> SendAllAsync(CancellationToken cancellationToken = default)
    {
        var all = await _repository.LoadAllProductsAsync(cancellationToken);
        if (!all.Any())
            return new SendResult(0, 0, 0, new());

        return await SendBatchesAsync(all, delayMs: 900, cancellationToken);
    }

    /// <summary>
    /// Carga productos Pendiente, los batca, envía cada lote, actualiza BD.
    /// Delay 100ms ya que típicamente son pocos productos (cambios).
    /// </summary>
    public async Task<SendResult> SendPendingAsync(CancellationToken cancellationToken = default)
    {
        var pending = await _repository.LoadPendingProductsAsync(cancellationToken);
        if (!pending.Any())
            return new SendResult(0, 0, 0, new());

        return await SendBatchesAsync(pending, delayMs: 100, cancellationToken);
    }

    private async Task<SendResult> SendBatchesAsync(IEnumerable<ProductoApi> products, int delayMs, CancellationToken cancellationToken)
    {
        int sent = 0, failed = 0;
        var failedCodes = new List<string>();
        var all = products.ToList();

        var batches = all.Chunk(BatchSize).ToList();

        for (int i = 0; i < batches.Count; i++)
        {
            var batch = batches[i];
            try
            {
                var response = await _client.ImportProductsAsync(batch, cancellationToken);

                if (response.Success)
                {
                    var codes = batch.Select(p => p.Code).ToList();
                    await _repository.MarkAsSentAsync(codes, DateTime.UtcNow, cancellationToken);
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

                        await _repository.MarkAsErrorAsync(product.Code, errorMsg, cancellationToken);
                        failedCodes.Add(product.Code);
                    }
                    failed += batch.Length;
                }
            }
            catch (Exception ex)
            {
                foreach (var product in batch)
                {
                    await _repository.MarkAsErrorAsync(product.Code, ex.Message, cancellationToken);
                    failedCodes.Add(product.Code);
                }
                failed += batch.Length;
            }

            if (i < batches.Count - 1)
                await Task.Delay(delayMs, cancellationToken);
        }

        return new SendResult(sent, failed, all.Count, failedCodes);
    }
}

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
    private const int BatchSize = 500;

    public SendService(IProductoRepository repository, IComprasParaguaiClient client)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <summary>
    /// Carga TODOS los productos, los batca, envía cada lote, marca como Enviado en BD.
    /// Usado para envío full inicial a Compras Paraguai.
    /// </summary>
    public async Task<SendResult> SendAllAsync(CancellationToken cancellationToken = default)
    {
        var all = await _repository.LoadAllProductsAsync(cancellationToken);
        if (!all.Any())
            return new SendResult(0, 0, 0, new());

        int sent = 0, failed = 0;
        var failedCodes = new List<string>();

        var batches = all
            .Chunk(BatchSize)
            .ToList();

        foreach (var batch in batches)
        {
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

                        var validationError = response.ValidationErrors
                            .FirstOrDefault(e => e.Code == product.Code);
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
        }

        return new SendResult(sent, failed, all.Count, failedCodes);
    }

    /// <summary>
    /// Carga productos Pendiente, los batca, envía cada lote, actualiza BD.
    /// </summary>
    public async Task<SendResult> SendPendingAsync(CancellationToken cancellationToken = default)
    {
        // 1. Cargar Pendientes.
        var pending = await _repository.LoadPendingProductsAsync(cancellationToken);
        if (!pending.Any())
            return new SendResult(0, 0, 0, new());

        int sent = 0, failed = 0;
        var failedCodes = new List<string>();

        // 2. Partir en lotes de 500.
        var batches = pending
            .Chunk(BatchSize)
            .ToList();

        foreach (var batch in batches)
        {
            try
            {
                // 3. Enviar lote.
                var response = await _client.ImportProductsAsync(batch, cancellationToken);

                if (response.Success)
                {
                    // OK: marcar como Enviado.
                    var codes = batch.Select(p => p.Code).ToList();
                    await _repository.MarkAsSentAsync(codes, DateTime.UtcNow, cancellationToken);
                    sent += batch.Length;
                }
                else
                {
                    // Falló: marcar como Error.
                    foreach (var product in batch)
                    {
                        string errorMsg = response.Message ?? "Error desconocido";

                        // Si hay errores de validación específicos, incluir el primero.
                        var validationError = response.ValidationErrors
                            .FirstOrDefault(e => e.Code == product.Code);
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
                // Excepción: marcar todos como Error con el mensaje de excepción.
                foreach (var product in batch)
                {
                    await _repository.MarkAsErrorAsync(product.Code, ex.Message, cancellationToken);
                    failedCodes.Add(product.Code);
                }

                failed += batch.Length;
            }
        }

        return new SendResult(sent, failed, pending.Count, failedCodes);
    }
}

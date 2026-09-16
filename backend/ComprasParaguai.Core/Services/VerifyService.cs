using ComprasParaguai.Core.Interfaces;
using ComprasParaguai.Core.Models;

namespace ComprasParaguai.Core.Services;

/// <summary>Resultado de comparar un producto local (marcado Enviado) con lo que tiene el destino.</summary>
public record VerifyResult(ProductoApi Local, RemoteProduct? Remote)
{
    public bool Found => Remote is not null;
    public bool PriceMatch => Remote?.Price is { } p && p == Local.Price;
    public bool StockMatch => Remote?.Stock is { } s && (int)s == Local.Stock;
}

/// <summary>
/// Toma N productos al azar marcados como Enviado para un destino y los busca en la API de ese destino.
/// Si el destino los devuelve, el envío llegó. Registra la corrida en verify_runs (si hay repositorio).
/// </summary>
public class VerifyService
{
    private readonly IProductoRepository _repository;
    private readonly IProductQueryClient _client;
    private readonly SyncTarget _target;
    private readonly IVerifyRunRepository? _verifyRuns;

    public VerifyService(IProductoRepository repository, IProductQueryClient client, SyncTarget target, IVerifyRunRepository? verifyRuns = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _target = target;
        _verifyRuns = verifyRuns;
    }

    public async Task<List<VerifyResult>> VerifyAsync(int count, int delayMs, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        long? runId = _verifyRuns is null ? null : await _verifyRuns.StartAsync(_target, count, cancellationToken);
        try
        {
            var sample = await _repository.LoadRandomSentProductsAsync(count, _target, cancellationToken);
            var results = new List<VerifyResult>(sample.Count);

            for (int i = 0; i < sample.Count; i++)
            {
                var local = sample[i];
                var remote = await _client.GetByCodeAsync(local.Code, cancellationToken);
                results.Add(new VerifyResult(local, remote));
                progress?.Report(i + 1);

                if (delayMs > 0 && i < sample.Count - 1)
                    await Task.Delay(delayMs, cancellationToken);
            }

            if (runId is { } id)
                await _verifyRuns!.FinishAsync(id, results, null, cancellationToken);
            return results;
        }
        catch
        {
            if (runId is { } id)
                await _verifyRuns!.FinishAsync(id, new List<VerifyResult>(), null, cancellationToken);
            throw;
        }
    }
}

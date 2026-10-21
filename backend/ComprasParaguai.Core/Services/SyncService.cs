using ComprasParaguai.Core.Interfaces;
using ComprasParaguai.Core.Models;

namespace ComprasParaguai.Core.Services;

/// <summary>
/// Orquesta la sincronización: lee vista → mapea → compara en BD → guarda.
/// Flujo (Sección 5 del plan):
/// 1. Leer vista en streaming
/// 2. Mapear + validar (rechazar inválidos)
/// 3. Calcular hash
/// 4. Comparar en memoria contra BD (cargada de una sola query)
/// 5. Guardar nuevos/modificados
/// </summary>
public class SyncService : ISyncService
{
    private readonly IViewReader _viewReader;
    private readonly IProductoRepository _repository;
    private readonly ProductoMapperService _mapper;
    private readonly int _historialMesesRetencion;

    public SyncService(
        IViewReader viewReader,
        IProductoRepository repository,
        ProductoMapperService mapper,
        int historialMesesRetencion = 6)
    {
        _viewReader = viewReader ?? throw new ArgumentNullException(nameof(viewReader));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _historialMesesRetencion = historialMesesRetencion;
    }

    /// <summary>
    /// Ejecuta una pasada de sincronización (full scan).
    /// Carga hash map de BD, lee vista, mapea, compara, guarda.
    /// </summary>
    public async Task<SyncResult> SyncAsync(CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        // 1. Cargar hash map de BD (una sola query).
        var bdHashMap = await _repository.LoadProductHashMapAsync(cancellationToken);

        // 2. Leer vista, mapear, compara, acumular cambios.
        int newCount = 0, updatedCount = 0, unchangedCount = 0, invalidCount = 0;
        var productsToSave = new List<ProductoApi>();

        await foreach (var view in _viewReader.ReadAsync(cancellationToken))
        {
            // Mapear y validar.
            var api = _mapper.MapAndValidate(view);
            if (api == null)
            {
                invalidCount++;
                continue;
            }

            // Comparar en memoria.
            if (bdHashMap.TryGetValue(api.Code, out var existingHash))
            {
                if (api.SourceHash == existingHash)
                {
                    // Sin cambios.
                    unchangedCount++;
                    continue;
                }

                // Cambió el hash → modificado.
                updatedCount++;
            }
            else
            {
                // No existe → nuevo.
                newCount++;
            }

            // Acumular para guardar.
            productsToSave.Add(api);
        }

        // 3. Guardar cambios en bloque.
        if (productsToSave.Any())
        {
            var now = DateTime.UtcNow;
            await _repository.UpsertProductsAsync(productsToSave, now, CambioContexto.Sync, cancellationToken);
        }

        int total = newCount + updatedCount + unchangedCount + invalidCount;
        return new SyncResult(newCount, updatedCount, unchangedCount, invalidCount, total);
    }

    /// <summary>
    /// Reconciliación diaria: detecta bajas (productos que ya no están en la vista).
    /// Marca con stock=0 y sync_status=Pendiente.
    /// </summary>
    public async Task ReconcileAsync(CancellationToken cancellationToken = default)
    {
        // Leer todos los códigos actuales en la vista.
        var codesInView = new HashSet<string>();
        await foreach (var view in _viewReader.ReadAsync(cancellationToken))
        {
            if (!string.IsNullOrWhiteSpace(view.Code))
                codesInView.Add(view.Code);
        }

        // Cargar códigos de BD.
        var bdHashMap = await _repository.LoadProductHashMapAsync(cancellationToken);
        var codesInBd = bdHashMap.Keys.ToHashSet();

        // Detectar bajas: en BD pero no en vista.
        var codesToMarkAsUnavailable = codesInBd.Except(codesInView).ToList();

        if (codesToMarkAsUnavailable.Any())
        {
            var now = DateTime.UtcNow;
            await _repository.MarkAsUnavailableAsync(codesToMarkAsUnavailable, now, CambioContexto.Reconciliacion, cancellationToken);
        }

        // Marcas y categorías sin productos pasan a inactivas (no se borran: conservan sus flags).
        await _repository.RefreshCatalogoActivoAsync(cancellationToken);

        // Historial de cambios: lo más viejo que N meses pasa al archivo (0 o menos = no archivar).
        if (_historialMesesRetencion > 0)
            await _repository.ArchivarHistorialAsync(_historialMesesRetencion, cancellationToken);
    }
}

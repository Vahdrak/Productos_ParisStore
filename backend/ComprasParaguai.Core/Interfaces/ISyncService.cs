using ComprasParaguai.Core.Models;

namespace ComprasParaguai.Core.Interfaces;

/// <summary>
/// Resultado de una pasada de sincronización.
/// </summary>
public record SyncResult(
    int NewProducts,
    int UpdatedProducts,
    int UnchangedProducts,
    int InvalidProducts,
    int TotalRows
);

/// <summary>
/// Servicio de sincronización: compara vista vs BD, detecta cambios.
/// Orquesta: lectura Oracle → mapeo → comparación en memoria → escritura.
/// </summary>
public interface ISyncService
{
    /// <summary>
    /// Ejecuta una pasada de sincronización (full scan).
    /// Lee la vista → mapea → compara con BD → guarda cambios.
    /// Retorna estadísticas (nuevos, modificados, sin cambios, inválidos).
    /// </summary>
    /// <param name="cancellationToken">Token para cancelar.</param>
    Task<SyncResult> SyncAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Ejecuta reconciliación diaria (detecta bajas no capturadas en sync incremental).
    /// Marca productos que ya no están en la vista con stock=0, sync_status=Pendiente.
    /// </summary>
    Task ReconcileAsync(CancellationToken cancellationToken = default);
}

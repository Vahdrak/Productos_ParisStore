using ComprasParaguai.Core.Models;

namespace ComprasParaguai.Core.Interfaces;

/// <summary>
/// Lee productos desde la vista de Cangrejo (SENSEI.PARAGUAY_COMPRAS_VIEW).
/// Intercambiable: hoy lee todo (escaneo completo), mañana puede ser incremental
/// si Cangrejo agrega columna de versión (Sección 2.4 del plan).
/// </summary>
public interface IViewReader
{
    /// <summary>
    /// Lee filas de la vista en streaming (sin cargar todo en memoria de golpe).
    /// Retorna un enumerador que carga fila por fila.
    /// </summary>
    /// <param name="cancellationToken">Token para cancelar la lectura.</param>
    /// <returns>IAsyncEnumerable de ProductoView — streaming de la vista.</returns>
    IAsyncEnumerable<ProductoView> ReadAsync(CancellationToken cancellationToken = default);
}

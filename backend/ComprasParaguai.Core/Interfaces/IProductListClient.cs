using ComprasParaguai.Core.Models;

namespace ComprasParaguai.Core.Interfaces;

/// <summary>Listado paginado de los productos que un destino ya tiene cargados (para auditar su disponibilidad).</summary>
public interface IProductListClient
{
    /// <summary>
    /// Recorre todas las páginas de la lista del destino con los filtros dados (null = sin filtrar).
    /// <paramref name="delayMs"/> es la pausa entre páginas.
    /// </summary>
    Task<List<RemoteProduct>> ListAsync(bool? available, bool? withStock, int delayMs, CancellationToken cancellationToken = default);

    /// <summary>Cuántos productos cumplen el filtro, según el total que informa el destino.</summary>
    Task<int> CountAsync(bool? available, bool? withStock, CancellationToken cancellationToken = default);
}

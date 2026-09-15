using ComprasParaguai.Core.Models;

namespace ComprasParaguai.Core.Interfaces;

/// <summary>Consulta de productos ya cargados en un destino (para verificar envíos).</summary>
public interface IProductQueryClient
{
    /// <summary>Busca un producto por código. Null si el destino no lo tiene.</summary>
    Task<RemoteProduct?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
}

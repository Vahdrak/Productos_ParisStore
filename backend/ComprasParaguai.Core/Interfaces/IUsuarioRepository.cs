using ComprasParaguai.Core.Models;

namespace ComprasParaguai.Core.Interfaces;

/// <summary>Usuarios del panel para el login.</summary>
public interface IUsuarioRepository
{
    /// <summary>Busca por nombre de login, sin distinguir mayúsculas.</summary>
    Task<Usuario?> GetByLoginAsync(string login, CancellationToken cancellationToken = default);

    Task<int> CountAsync(CancellationToken cancellationToken = default);

    Task CreateAsync(Usuario usuario, CancellationToken cancellationToken = default);

    Task RegistrarLoginAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Reemplaza el hash de la contraseña del usuario.</summary>
    Task CambiarPasswordAsync(int id, string passwordHash, CancellationToken cancellationToken = default);
}

using ComprasParaguai.Core.Interfaces;
using ComprasParaguai.Core.Models;
using Dapper;
using Npgsql;

namespace ComprasParaguai.Infrastructure.Repositories;

/// <summary>Usuarios del panel en PostgreSQL.</summary>
public class PostgreSqlUsuarioRepository : IUsuarioRepository
{
    private readonly string _connectionString;

    public PostgreSqlUsuarioRepository(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task<Usuario?> GetByLoginAsync(string login, CancellationToken cancellationToken = default)
    {
        const string query = @"
            SELECT id AS Id, usuario AS Login, nombre AS Nombre, password_hash AS PasswordHash, activo AS Activo
            FROM usuarios
            WHERE lower(usuario) = lower(@login)";

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return await connection.QueryFirstOrDefaultAsync<Usuario>(query, new { login });
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM usuarios");
    }

    public async Task CreateAsync(Usuario usuario, CancellationToken cancellationToken = default)
    {
        const string query = @"
            INSERT INTO usuarios (usuario, nombre, password_hash, activo)
            VALUES (@Login, @Nombre, @PasswordHash, @Activo)";

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(query, usuario);
    }

    public async Task RegistrarLoginAsync(int id, CancellationToken cancellationToken = default)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync("UPDATE usuarios SET last_login_at = now() WHERE id = @id", new { id });
    }

    public async Task CambiarPasswordAsync(int id, string passwordHash, CancellationToken cancellationToken = default)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync("UPDATE usuarios SET password_hash = @passwordHash WHERE id = @id", new { id, passwordHash });
    }
}

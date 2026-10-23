using ComprasParaguai.Core.Interfaces;
using Dapper;
using Npgsql;

namespace ComprasParaguai.Infrastructure.Repositories;

public class PostgreSqlVerifySolicitudRepository : IVerifySolicitudRepository
{
    private const string Columnas = @"
        id AS Id, tipo AS Tipo, estado AS Estado, origen AS Origen, solicitado_por AS SolicitadoPor,
        solicitado_at AS SolicitadoAt, iniciado_at AS IniciadoAt, terminado_at AS TerminadoAt,
        resultado AS Resultado, error AS Error";

    private readonly string _connectionString;

    public PostgreSqlVerifySolicitudRepository(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task<(VerifySolicitudDto Solicitud, bool Nueva)> SolicitarAsync(string tipo, string origen, string? usuario, CancellationToken cancellationToken = default)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // El índice único parcial deja a lo sumo un pedido abierto por tipo: si ya hay uno, no se inserta nada.
        var creada = await connection.QuerySingleOrDefaultAsync<VerifySolicitudDto>($@"
            INSERT INTO verify_solicitudes (tipo, origen, solicitado_por)
            VALUES (@tipo, @origen, @usuario)
            ON CONFLICT (tipo) WHERE estado IN ('pendiente', 'corriendo') DO NOTHING
            RETURNING {Columnas}", new { tipo, origen, usuario });
        if (creada is not null) return (creada, true);

        var abierta = await connection.QueryFirstAsync<VerifySolicitudDto>($@"
            SELECT {Columnas} FROM verify_solicitudes
            WHERE tipo = @tipo AND estado IN ('pendiente', 'corriendo')
            ORDER BY solicitado_at LIMIT 1", new { tipo });
        return (abierta, false);
    }

    public async Task<VerifySolicitudDto?> ReclamarSiguienteAsync(CancellationToken cancellationToken = default)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<VerifySolicitudDto>($@"
            UPDATE verify_solicitudes SET estado = 'corriendo', iniciado_at = now()
            WHERE id = (SELECT id FROM verify_solicitudes WHERE estado = 'pendiente'
                        ORDER BY solicitado_at LIMIT 1 FOR UPDATE SKIP LOCKED)
            RETURNING {Columnas}");
    }

    public async Task TerminarAsync(long id, string? resultado, string? error, CancellationToken cancellationToken = default)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(@"
            UPDATE verify_solicitudes
            SET estado = CASE WHEN @error IS NULL THEN 'ok' ELSE 'error' END,
                terminado_at = now(), resultado = @resultado, error = @error
            WHERE id = @id", new { id, resultado, error });
    }

    public async Task<int> MarcarInterrumpidasAsync(CancellationToken cancellationToken = default)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return await connection.ExecuteAsync(@"
            UPDATE verify_solicitudes
            SET estado = 'error', terminado_at = now(), error = 'Interrumpida: el worker se detuvo antes de terminar'
            WHERE estado = 'corriendo'");
    }

    public async Task<DateTime?> UltimaSolicitudAsync(string tipo, CancellationToken cancellationToken = default)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<DateTime?>(
            "SELECT max(solicitado_at) FROM verify_solicitudes WHERE tipo = @tipo", new { tipo });
    }

    public async Task<List<VerifySolicitudDto>> ListarAsync(int limit = 10, CancellationToken cancellationToken = default)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return (await connection.QueryAsync<VerifySolicitudDto>(
            $"SELECT {Columnas} FROM verify_solicitudes ORDER BY solicitado_at DESC LIMIT @limit",
            new { limit = Math.Clamp(limit, 1, 50) })).ToList();
    }
}

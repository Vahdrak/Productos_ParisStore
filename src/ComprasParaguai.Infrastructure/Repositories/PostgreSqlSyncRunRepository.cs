using ComprasParaguai.Core.Interfaces;
using ComprasParaguai.Core.Models;
using Dapper;
using Npgsql;

namespace ComprasParaguai.Infrastructure.Repositories;

/// <summary>
/// Repositorio de bitácora (sync_runs) en PostgreSQL.
/// </summary>
public class PostgreSqlSyncRunRepository : ISyncRunRepository
{
    private readonly string _connectionString;

    public PostgreSqlSyncRunRepository(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    /// <summary>
    /// Inicia una corrida: inserta fila con started_at, retorna ID.
    /// </summary>
    public async Task<long> StartSyncAsync(CancellationToken cancellationToken = default)
    {
        const string query = @"
            INSERT INTO sync_runs (started_at)
            VALUES (@now)
            RETURNING id
        ";

        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync(cancellationToken);
            var id = await connection.QueryFirstAsync<long>(
                query,
                new { now = DateTime.UtcNow },
                commandTimeout: 30);

            return id;
        }
    }

    /// <summary>
    /// Cierra una corrida: actualiza con finished_at, contadores y notas.
    /// </summary>
    public async Task FinishSyncAsync(
        long syncRunId,
        SyncResult syncResult,
        SendResult sendResult,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        const string query = @"
            UPDATE sync_runs
            SET
                finished_at = @now,
                rows_in_view = @rowsInView,
                new_count = @newCount,
                updated_count = @updatedCount,
                sent = @sent,
                failed = @failed,
                notes = @notes
            WHERE id = @id
        ";

        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync(cancellationToken);
            await connection.ExecuteAsync(
                query,
                new
                {
                    now = DateTime.UtcNow,
                    rowsInView = syncResult.TotalRows,
                    newCount = syncResult.NewProducts,
                    updatedCount = syncResult.UpdatedProducts,
                    sent = sendResult.Sent,
                    failed = sendResult.Failed,
                    notes,
                    id = syncRunId
                },
                commandTimeout: 30);
        }
    }

    /// <summary>
    /// Obtiene fecha de la última corrida exitosa (finished_at no null).
    /// </summary>
    public async Task<DateTime?> GetLastSuccessfulSyncAsync(CancellationToken cancellationToken = default)
    {
        const string query = @"
            SELECT MAX(finished_at) FROM sync_runs
            WHERE finished_at IS NOT NULL
        ";

        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync(cancellationToken);
            var result = await connection.QueryFirstOrDefaultAsync<DateTime?>(
                query,
                commandTimeout: 30);

            return result;
        }
    }
}

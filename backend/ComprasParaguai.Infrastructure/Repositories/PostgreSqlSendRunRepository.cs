using ComprasParaguai.Core.Interfaces;
using ComprasParaguai.Core.Models;
using Dapper;
using Npgsql;

namespace ComprasParaguai.Infrastructure.Repositories;

public class PostgreSqlSendRunRepository : ISendRunRepository
{
    private readonly string _connectionString;

    public PostgreSqlSendRunRepository(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task<long> StartAsync(SyncTarget target, string mode, CancellationToken cancellationToken = default)
    {
        const string sql = "INSERT INTO send_runs (target, mode, started_at) VALUES (@target, @mode, @now) RETURNING id";
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<long>(sql, new { target = target.ToString(), mode, now = DateTime.UtcNow });
    }

    public async Task FinishAsync(long runId, SendResult result, string? error = null, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE send_runs
            SET finished_at = @now, total = @total, sent = @sent, failed = @failed, error = @error
            WHERE id = @runId";
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(sql, new
        {
            runId, now = DateTime.UtcNow, total = result.Total, sent = result.Sent, failed = result.Failed,
            error = error is null ? null : (error.Length > 2000 ? error[..2000] : error)
        });
    }

    public async Task<List<SendRunDto>> GetRunsAsync(SyncTarget? target = null, int limit = 50, CancellationToken cancellationToken = default)
    {
        string sql = @"
            SELECT id AS Id, target AS Target, mode AS Mode, started_at AS StartedAt, finished_at AS FinishedAt,
                   total AS Total, sent AS Sent, failed AS Failed, error AS Error
            FROM send_runs
            " + (target is null ? "" : "WHERE target = @target ") + @"
            ORDER BY started_at DESC
            LIMIT @limit";
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return (await connection.QueryAsync<SendRunDto>(sql, new { target = target?.ToString(), limit })).ToList();
    }
}

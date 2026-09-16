using ComprasParaguai.Core.Interfaces;
using ComprasParaguai.Core.Models;
using ComprasParaguai.Core.Services;
using Dapper;
using Npgsql;

namespace ComprasParaguai.Infrastructure.Repositories;

public class PostgreSqlVerifyRunRepository : IVerifyRunRepository
{
    private readonly string _connectionString;

    public PostgreSqlVerifyRunRepository(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task<long> StartAsync(SyncTarget target, int sampleSize, CancellationToken cancellationToken = default)
    {
        const string sql = "INSERT INTO verify_runs (target, started_at, sample_size) VALUES (@target, @now, @sampleSize) RETURNING id";
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<long>(sql, new { target = target.ToString(), now = DateTime.UtcNow, sampleSize });
    }

    public async Task FinishAsync(long runId, IReadOnlyList<VerifyResult> results, string? csvPath, CancellationToken cancellationToken = default)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        using var tx = await connection.BeginTransactionAsync(cancellationToken);

        const string insert = @"
            INSERT INTO verify_results (run_id, code, name_pt, found, price_local, price_api, stock_local, stock_api, available_api, updated_at_api)
            VALUES (@runId, @code, @namePt, @found, @priceLocal, @priceApi, @stockLocal, @stockApi, @availableApi, @updatedAtApi)
            ON CONFLICT (run_id, code) DO NOTHING";

        await connection.ExecuteAsync(insert, results.Select(r => new
        {
            runId,
            code = r.Local.Code,
            namePt = r.Local.NamePt,
            found = r.Found,
            priceLocal = r.Local.Price,
            priceApi = r.Remote?.Price,
            stockLocal = r.Local.Stock,
            stockApi = r.Remote?.Stock,
            availableApi = r.Remote?.Available,
            updatedAtApi = r.Remote?.UpdatedAt
        }), tx);

        const string finish = @"
            UPDATE verify_runs
            SET finished_at = @now, sample_size = @sampleSize, found = @found, price_match = @priceMatch, stock_match = @stockMatch, csv_path = @csvPath
            WHERE id = @runId";
        await connection.ExecuteAsync(finish, new
        {
            runId, now = DateTime.UtcNow, sampleSize = results.Count,
            found = results.Count(r => r.Found),
            priceMatch = results.Count(r => r.PriceMatch),
            stockMatch = results.Count(r => r.StockMatch),
            csvPath
        }, tx);

        await tx.CommitAsync(cancellationToken);
    }

    public async Task<List<VerifyRunDto>> GetRunsAsync(int limit = 20, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT id AS Id, target AS Target, started_at AS StartedAt, finished_at AS FinishedAt, sample_size AS SampleSize,
                   found AS Found, price_match AS PriceMatch, stock_match AS StockMatch, csv_path AS CsvPath
            FROM verify_runs ORDER BY started_at DESC LIMIT @limit";
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return (await connection.QueryAsync<VerifyRunDto>(sql, new { limit })).ToList();
    }

    public async Task<List<VerifyResultDto>> GetResultsAsync(long runId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT code AS Code, name_pt AS NamePt, found AS Found, price_local AS PriceLocal, price_api AS PriceApi,
                   stock_local AS StockLocal, stock_api AS StockApi, available_api AS AvailableApi, updated_at_api AS UpdatedAtApi
            FROM verify_results WHERE run_id = @runId ORDER BY found, code";
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return (await connection.QueryAsync<VerifyResultDto>(sql, new { runId })).ToList();
    }
}

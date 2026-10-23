using ComprasParaguai.Core.Interfaces;
using ComprasParaguai.Core.Models;
using Dapper;
using Npgsql;

namespace ComprasParaguai.Infrastructure.Repositories;

public class PostgreSqlDisponibilidadRepository : IDisponibilidadRepository
{
    private readonly string _connectionString;

    public PostgreSqlDisponibilidadRepository(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task ReemplazarAsync(IReadOnlyList<RemoteProduct> noDisponibles, int totalConStock, DateTime checkedAt, CancellationToken cancellationToken = default)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        using var tx = await connection.BeginTransactionAsync(cancellationToken);

        await connection.ExecuteAsync("DELETE FROM compras_disponibilidad", transaction: tx);

        if (noDisponibles.Count > 0)
        {
            const string insert = @"
                INSERT INTO compras_disponibilidad (code, stock_api, updated_at_api, checked_at)
                VALUES (@code, @stockApi, @updatedAtApi, @checkedAt)
                ON CONFLICT (code) DO NOTHING";
            await connection.ExecuteAsync(insert, noDisponibles.Select(p => new
            {
                code = p.Code,
                stockApi = p.Stock,
                updatedAtApi = p.UpdatedAt,
                checkedAt
            }), tx);
        }

        const string corrida = @"
            INSERT INTO compras_disponibilidad_corrida (id, checked_at, total_con_stock, no_disponibles)
            VALUES (1, @checkedAt, @totalConStock, @noDisponibles)
            ON CONFLICT (id) DO UPDATE
            SET checked_at = EXCLUDED.checked_at, total_con_stock = EXCLUDED.total_con_stock, no_disponibles = EXCLUDED.no_disponibles";
        await connection.ExecuteAsync(corrida, new { checkedAt, totalConStock, noDisponibles = noDisponibles.Count }, tx);

        await tx.CommitAsync(cancellationToken);
    }

    public async Task<NoDisponiblesDto> ListarAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // Solo los que todavía tienen stock local: si lo dejamos en 0, que Compras lo marque no disponible es lo esperado.
        const string from = @"
            FROM compras_disponibilidad d
            JOIN productos p ON p.code = d.code
            LEFT JOIN marcas m ON m.id = p.marca_id
            WHERE p.stock > 0";

        var total = await connection.ExecuteScalarAsync<int>($"SELECT COUNT(*) {from}");

        var items = (await connection.QueryAsync<NoDisponibleItemDto>($@"
            SELECT p.code AS Code, p.name_pt AS NamePt, m.nombre AS Marca, p.price AS Price, p.stock AS StockLocal,
                   d.stock_api AS StockApi, d.updated_at_api AS UpdatedAtApi, p.link AS Link
            {from}
            ORDER BY p.price DESC, p.code
            LIMIT @pageSize OFFSET @offset", new { pageSize, offset = (page - 1) * pageSize })).ToList();

        var corrida = await connection.QuerySingleOrDefaultAsync<CorridaRow>(
            "SELECT checked_at AS CheckedAt, total_con_stock AS TotalConStock FROM compras_disponibilidad_corrida WHERE id = 1");

        return new NoDisponiblesDto
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize),
            CheckedAt = corrida?.CheckedAt,
            TotalConStock = corrida?.TotalConStock ?? 0
        };
    }

    private sealed class CorridaRow
    {
        public DateTime CheckedAt { get; set; }
        public int TotalConStock { get; set; }
    }

    public async Task<List<string>> CodigosAsync(CancellationToken cancellationToken = default)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return (await connection.QueryAsync<string>("SELECT code FROM compras_disponibilidad ORDER BY code")).ToList();
    }
}

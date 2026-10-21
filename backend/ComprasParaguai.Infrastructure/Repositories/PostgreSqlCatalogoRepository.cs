using ComprasParaguai.Core.Interfaces;
using Dapper;
using Npgsql;

namespace ComprasParaguai.Infrastructure.Repositories;

/// <summary>
/// Lectura del catálogo normalizado (categorias en árbol + marcas) en PostgreSQL.
/// </summary>
public class PostgreSqlCatalogoRepository : ICatalogoRepository
{
    private readonly string _connectionString;

    public PostgreSqlCatalogoRepository(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task<List<CategoriaNodoDto>> GetArbolCategoriasAsync(bool soloActivas = true, CancellationToken cancellationToken = default)
    {
        // sub = cada nodo con todos sus descendientes (y él mismo); así los conteos incluyen el subárbol.
        string query = $@"
            WITH RECURSIVE sub AS (
                SELECT c.id AS root_id, c.id AS node_id FROM categorias c
                UNION ALL
                SELECT s.root_id, c.id
                FROM sub s
                JOIN categorias c ON c.parent_id = s.node_id
            )
            SELECT
                c.id        AS Id,
                c.parent_id AS ParentId,
                c.nombre    AS Nombre,
                c.nivel     AS Nivel,
                c.ruta      AS Ruta,
                c.activo    AS Activo,
                COUNT(p.id)                                         AS Productos,
                COUNT(p.id) FILTER (WHERE p.sync_status = 0)         AS PendientesCompras,
                COUNT(p.id) FILTER (WHERE p.sync_compara_status = 0) AS PendientesCompara
            FROM categorias c
            JOIN sub s ON s.root_id = c.id
            LEFT JOIN productos p ON p.categoria_id = s.node_id
            {(soloActivas ? "WHERE c.activo" : "")}
            GROUP BY c.id
            ORDER BY c.nivel, c.nombre";

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var nodos = (await connection.QueryAsync<CategoriaNodoDto>(query)).ToList();

        // Armar el árbol en memoria (≈2000 nodos). Los nodos van ordenados por nivel, así que el padre ya existe.
        var porId = nodos.ToDictionary(n => n.Id);
        var raices = new List<CategoriaNodoDto>();
        foreach (var nodo in nodos)
        {
            if (nodo.ParentId.HasValue && porId.TryGetValue(nodo.ParentId.Value, out var padre))
                padre.Hijos.Add(nodo);
            else
                raices.Add(nodo);
        }

        return raices;
    }

    public async Task<MarcaListDto> GetMarcasAsync(string? search, bool soloActivas, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 500);

        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(search))
        {
            conditions.Add("m.nombre ILIKE @search");
            parameters.Add("search", $"%{search.Trim()}%");
        }

        if (soloActivas)
            conditions.Add("m.activo");

        string where = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";

        string query = $@"
            SELECT
                m.id     AS Id,
                m.nombre AS Nombre,
                m.activo AS Activo,
                COUNT(p.id)                                          AS Productos,
                COUNT(p.id) FILTER (WHERE p.sync_status = 0)          AS PendientesCompras,
                COUNT(p.id) FILTER (WHERE p.sync_compara_status = 0)  AS PendientesCompara
            FROM marcas m
            LEFT JOIN productos p ON p.marca_id = m.id
            {where}
            GROUP BY m.id
            ORDER BY COUNT(p.id) DESC, m.nombre
            LIMIT @pageSize OFFSET @offset";

        string countQuery = $"SELECT COUNT(*) FROM marcas m {where}";

        parameters.Add("pageSize", pageSize);
        parameters.Add("offset", (page - 1) * pageSize);

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var items = (await connection.QueryAsync<MarcaDto>(query, parameters)).ToList();
        var total = await connection.ExecuteScalarAsync<int>(countQuery, parameters);

        return new MarcaListDto
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize)
        };
    }
}

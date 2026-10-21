using System.Text.Json;
using ComprasParaguai.Core.Interfaces;
using Dapper;
using Npgsql;

namespace ComprasParaguai.Infrastructure.Repositories;

/// <summary>
/// Lectura del historial de cambios (productos_historial) con diff por campo.
/// </summary>
public class PostgreSqlHistorialRepository : IHistorialRepository
{
    private readonly string _connectionString;

    public PostgreSqlHistorialRepository(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    // Fila tal como sale de la consulta (cambios llega como texto JSON).
    private class CambioRow
    {
        public long Id { get; set; }
        public DateTime ChangedAt { get; set; }
        public string Code { get; set; } = string.Empty;
        public string? Nombre { get; set; }
        public string? Marca { get; set; }
        public string Operation { get; set; } = "U";
        public string? Origen { get; set; }
        public string? Usuario { get; set; }
        public string? Cambios { get; set; }
    }

    public async Task<CambioListDto> BuscarCambiosAsync(CambiosFiltro filtro, CancellationToken cancellationToken = default)
    {
        int page = Math.Max(1, filtro.Page);
        int pageSize = Math.Clamp(filtro.PageSize, 1, 200);

        // Solo cambios con algo que mostrar: altas, o cambios con al menos un campo seguido.
        var conditions = new List<string> { "(h.operation = 'I' OR h.cambios <> '{}'::jsonb)" };
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(filtro.Code))
        {
            conditions.Add("h.code = @code");
            parameters.Add("code", filtro.Code.Trim());
        }

        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            conditions.Add("(p.code ILIKE @texto OR p.name_es ILIKE @texto OR p.name_pt ILIKE @texto)");
            parameters.Add("texto", $"%{filtro.Texto.Trim()}%");
        }

        if (filtro.MarcaId.HasValue)
        {
            conditions.Add("h.marca_id = @marcaId");
            parameters.Add("marcaId", filtro.MarcaId.Value);
        }

        if (filtro.CategoriaId.HasValue)
        {
            conditions.Add(@"h.categoria_id IN (
                    SELECT sub.id
                    FROM categorias raiz
                    JOIN categorias sub ON sub.ruta = raiz.ruta OR left(sub.ruta, length(raiz.ruta) + 1) = raiz.ruta || '>'
                    WHERE raiz.id = @categoriaId)");
            parameters.Add("categoriaId", filtro.CategoriaId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Campo))
        {
            conditions.Add("h.cambios ? @campo");
            parameters.Add("campo", filtro.Campo.Trim());
        }

        if (!string.IsNullOrWhiteSpace(filtro.Origen))
        {
            conditions.Add("h.origen = @origen");
            parameters.Add("origen", filtro.Origen.Trim().ToLowerInvariant());
        }

        if (filtro.Desde.HasValue)
        {
            conditions.Add("h.changed_at >= @desde");
            parameters.Add("desde", filtro.Desde.Value.ToDateTime(TimeOnly.MinValue));
        }

        if (filtro.Hasta.HasValue)
        {
            // Inclusive: hasta el final del día indicado.
            conditions.Add("h.changed_at < @hasta");
            parameters.Add("hasta", filtro.Hasta.Value.AddDays(1).ToDateTime(TimeOnly.MinValue));
        }

        string where = "WHERE " + string.Join(" AND ", conditions);

        // El JOIN a productos solo hace falta para nombre y búsqueda por texto; el resto va por índices del historial.
        string query = $@"
            SELECT
                h.historial_id     AS Id,
                h.changed_at       AS ChangedAt,
                h.code             AS Code,
                p.name_es          AS Nombre,
                m.nombre           AS Marca,
                h.operation        AS Operation,
                h.origen           AS Origen,
                h.usuario          AS Usuario,
                h.cambios::text    AS Cambios
            FROM productos_historial h
            LEFT JOIN productos p ON p.id = h.producto_id
            LEFT JOIN marcas m ON m.id = h.marca_id
            {where}
            ORDER BY h.changed_at DESC, h.historial_id DESC
            LIMIT @pageSize OFFSET @offset";

        string countQuery = $@"
            SELECT COUNT(*)
            FROM productos_historial h
            LEFT JOIN productos p ON p.id = h.producto_id
            {where}";

        parameters.Add("pageSize", pageSize);
        parameters.Add("offset", (page - 1) * pageSize);

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var rows = (await connection.QueryAsync<CambioRow>(query, parameters)).ToList();
        var total = await connection.ExecuteScalarAsync<int>(countQuery, parameters);

        return new CambioListDto
        {
            Items = rows.Select(ToDto).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize)
        };
    }

    private static CambioDto ToDto(CambioRow row) => new()
    {
        Id = row.Id,
        ChangedAt = row.ChangedAt,
        Code = row.Code,
        Nombre = row.Nombre,
        Marca = row.Marca,
        Operacion = row.Operation == "I" ? "Alta" : "Cambio",
        Origen = row.Origen,
        Usuario = row.Usuario,
        Cambios = ParseCambios(row.Cambios)
    };

    /// <summary>{"price": {"antes": "10", "despues": "12"}} → lista de campos.</summary>
    private static List<CampoCambioDto> ParseCambios(string? json)
    {
        var lista = new List<CampoCambioDto>();
        if (string.IsNullOrWhiteSpace(json))
            return lista;

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            return lista;

        foreach (var campo in doc.RootElement.EnumerateObject())
        {
            lista.Add(new CampoCambioDto
            {
                Campo = campo.Name,
                Antes = ValorTexto(campo.Value, "antes"),
                Despues = ValorTexto(campo.Value, "despues")
            });
        }

        return lista;
    }

    private static string? ValorTexto(JsonElement campo, string nombre) =>
        campo.ValueKind == JsonValueKind.Object && campo.TryGetProperty(nombre, out var v) && v.ValueKind != JsonValueKind.Null
            ? v.ToString()
            : null;
}

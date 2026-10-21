using ComprasParaguai.Core.Interfaces;
using ComprasParaguai.Core.Models;
using ComprasParaguai.Core.Services;
using Dapper;
using Npgsql;

namespace ComprasParaguai.Infrastructure.Repositories;

/// <summary>
/// Productos en Error de un destino (status = 2), agrupados por tipo de error, marca y departamento.
/// Se leen todos los errores (hay cientos o pocos miles) y se agrupan en memoria: el tipo de error sale de clasificar
/// el mensaje (ErrorClasificador), que no es una columna de la BD.
/// </summary>
public class PostgreSqlErrorEnvioRepository : IErrorEnvioRepository
{
    private const int MaxFilas = 50_000;
    private const string SinCategoria = "(sin categoría)";

    private readonly string _connectionString;

    public PostgreSqlErrorEnvioRepository(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    private static (string Status, string Error) Cols(SyncTarget target) => target switch
    {
        SyncTarget.ComprasParaguai => ("sync_status", "last_error"),
        SyncTarget.ComparaParaguai => ("sync_compara_status", "last_error_compara"),
        _ => throw new ArgumentOutOfRangeException(nameof(target), target, null)
    };

    private class Fila
    {
        public string Code { get; set; } = string.Empty;
        public string? Nombre { get; set; }
        public int MarcaId { get; set; }
        public string? Marca { get; set; }
        public string? CategoriaRuta { get; set; }
        public string? Mensaje { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Calculados al clasificar
        public ErrorTipo Tipo { get; set; } = ErrorClasificador.Otro;
        public bool EsRetiro { get; set; }

        public string Departamento
        {
            get
            {
                var segs = (CategoriaRuta ?? string.Empty).Split('>');
                return segs.Length > 1 ? segs[1] : SinCategoria;
            }
        }
    }

    /// <summary>Lee y clasifica todos los errores del destino (solo con el filtro de texto aplicado en SQL).</summary>
    private async Task<(List<Fila> Filas, string? RutaRaiz, Dictionary<string, int> Departamentos)> CargarAsync(
        SyncTarget target, ErroresFiltro filtro, CancellationToken cancellationToken)
    {
        var cols = Cols(target);
        var parametros = new DynamicParameters();
        string texto = "";
        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            texto = "AND (p.code ILIKE @texto OR p.name_es ILIKE @texto)";
            parametros.Add("texto", $"%{filtro.Texto.Trim()}%");
        }

        string query = $@"
            SELECT p.code AS Code, p.name_es AS Nombre, p.marca_id AS MarcaId, m.nombre AS Marca,
                   c.ruta AS CategoriaRuta, p.{cols.Error} AS Mensaje, p.updated_at AS UpdatedAt
            FROM productos p
            JOIN marcas m ON m.id = p.marca_id
            LEFT JOIN categorias c ON c.id = p.categoria_id
            WHERE p.{cols.Status} = 2 {texto}
            ORDER BY p.updated_at DESC, p.code
            LIMIT {MaxFilas}";

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var filas = (await connection.QueryAsync<Fila>(query, parametros)).ToList();
        foreach (var fila in filas)
        {
            fila.Tipo = ErrorClasificador.Clasificar(fila.Mensaje, out bool esRetiro);
            fila.EsRetiro = esRetiro;
        }

        string? rutaRaiz = filtro.CategoriaId is { } catId
            ? await connection.ExecuteScalarAsync<string?>("SELECT ruta FROM categorias WHERE id = @catId", new { catId })
            : null;

        var departamentos = (await connection.QueryAsync<(int Id, string Nombre)>("SELECT id, nombre FROM categorias WHERE nivel = 2"))
            .GroupBy(d => d.Nombre).ToDictionary(g => g.Key, g => g.First().Id);

        return (filas, rutaRaiz, departamentos);
    }

    private static bool EnCategoria(Fila f, string? rutaRaiz) =>
        rutaRaiz is null
        || (f.CategoriaRuta is not null && (f.CategoriaRuta == rutaRaiz || f.CategoriaRuta.StartsWith(rutaRaiz + ">", StringComparison.Ordinal)));

    /// <summary>Aplica los filtros en memoria; <paramref name="ignorar"/> deja fuera uno ("tipo", "marca" o "categoria").</summary>
    private static IEnumerable<Fila> Aplicar(IEnumerable<Fila> filas, ErroresFiltro filtro, string? rutaRaiz, string? ignorar)
    {
        if (ignorar != "marca" && filtro.MarcaId is { } marcaId)
            filas = filas.Where(f => f.MarcaId == marcaId);
        if (ignorar != "categoria" && filtro.CategoriaId is not null)
            filas = filas.Where(f => EnCategoria(f, rutaRaiz));
        if (ignorar != "tipo" && !string.IsNullOrWhiteSpace(filtro.Tipo))
            filas = filas.Where(f => string.Equals(f.Tipo.Codigo, filtro.Tipo.Trim(), StringComparison.OrdinalIgnoreCase));
        return filas;
    }

    public async Task<ErroresResultadoDto> BuscarAsync(SyncTarget target, ErroresFiltro filtro, CancellationToken cancellationToken = default)
    {
        int page = Math.Max(1, filtro.Page);
        int pageSize = Math.Clamp(filtro.PageSize, 1, 200);
        var (filas, rutaRaiz, departamentos) = await CargarAsync(target, filtro, cancellationToken);

        // Cada resumen deja fuera su propia dimensión, para poder cambiar de tipo / marca / departamento sin perder las otras opciones.
        var tipos = Aplicar(filas, filtro, rutaRaiz, "tipo")
            .GroupBy(f => f.Tipo)
            .Select(g => new ErrorTipoResumenDto
            {
                Codigo = g.Key.Codigo, Titulo = g.Key.Titulo, QueHacer = g.Key.QueHacer,
                Reintentable = g.Key.Reintentable, Total = g.Count()
            })
            .OrderByDescending(t => t.Total).ToList();

        var porDepartamento = Aplicar(filas, filtro, rutaRaiz, "categoria")
            .GroupBy(f => f.Departamento)
            .Select(g => new ErrorGrupoDto
            {
                Id = departamentos.TryGetValue(g.Key, out var id) ? id : null,
                Nombre = g.Key, Total = g.Count()
            })
            .OrderByDescending(g => g.Total).ToList();

        var porMarca = Aplicar(filas, filtro, rutaRaiz, "marca")
            .GroupBy(f => (f.MarcaId, f.Marca))
            .Select(g => new ErrorGrupoDto { Id = g.Key.MarcaId, Nombre = g.Key.Marca ?? "?", Total = g.Count() })
            .OrderByDescending(g => g.Total).ThenBy(g => g.Nombre).Take(20).ToList();

        var filtradas = Aplicar(filas, filtro, rutaRaiz, null).ToList();
        var pagina = filtradas.Skip((page - 1) * pageSize).Take(pageSize).Select(f => new ErrorProductoDto
        {
            Code = f.Code,
            Nombre = f.Nombre,
            Marca = f.Marca,
            Categoria = f.CategoriaRuta is null ? null : string.Join(" > ", f.CategoriaRuta.Split('>').Skip(1)),
            Tipo = f.Tipo.Codigo,
            TipoTitulo = f.Tipo.Titulo,
            Etapa = f.EsRetiro ? "retiro" : "envio",
            Mensaje = f.Mensaje,
            UpdatedAt = f.UpdatedAt
        }).ToList();

        return new ErroresResultadoDto
        {
            Tipos = tipos,
            PorDepartamento = porDepartamento,
            PorMarca = porMarca,
            Items = pagina,
            TotalCount = filtradas.Count,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(filtradas.Count / (double)pageSize)
        };
    }

    public async Task<int> ReintentarAsync(SyncTarget target, ErroresFiltro filtro, IReadOnlyCollection<string>? codes, CancellationToken cancellationToken = default)
    {
        var cols = Cols(target);
        string[] aReiniciar;

        if (codes is { Count: > 0 })
        {
            aReiniciar = codes.ToArray();
        }
        else
        {
            var (filas, rutaRaiz, _) = await CargarAsync(target, filtro, cancellationToken);
            aReiniciar = Aplicar(filas, filtro, rutaRaiz, null).Select(f => f.Code).ToArray();
        }

        if (aReiniciar.Length == 0)
            return 0;

        // El UPDATE solo toca los que siguen en Error (si un envío los resolvió mientras tanto, no se pisan).
        string update = $@"
            UPDATE productos
            SET {cols.Status} = 0, {cols.Error} = NULL, updated_at = now()
            WHERE {cols.Status} = 2 AND code = ANY(@codes)";

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return await connection.ExecuteAsync(update, new { codes = aReiniciar });
    }
}

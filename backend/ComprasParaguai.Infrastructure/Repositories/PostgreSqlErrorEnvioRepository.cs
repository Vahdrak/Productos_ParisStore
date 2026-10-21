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

    private static string StatusCol(SyncTarget target) => target switch
    {
        SyncTarget.ComprasParaguai => "sync_status",
        SyncTarget.ComparaParaguai => "sync_compara_status",
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
        public DateTime Desde { get; set; }
        public int Intentos { get; set; } = 1;
        public string Etapa { get; set; } = "envio";

        // Calculados al clasificar
        public ErrorTipo Tipo { get; set; } = ErrorClasificador.Otro;

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
        string status = StatusCol(target);
        var parametros = new DynamicParameters();
        string texto = "";
        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            texto = "AND (p.code ILIKE @texto OR p.name_es ILIKE @texto)";
            parametros.Add("texto", $"%{filtro.Texto.Trim()}%");
        }

        // El error vigente de cada producto = su fallo abierto más reciente (envio_errores); el texto vive en error_mensajes.
        string query = $@"
            SELECT p.code AS Code, p.name_es AS Nombre, p.marca_id AS MarcaId, m.nombre AS Marca,
                   c.ruta AS CategoriaRuta, em.mensaje AS Mensaje,
                   COALESCE(e.ultimo_at, p.updated_at) AS UpdatedAt, COALESCE(e.desde, p.updated_at) AS Desde,
                   COALESCE(e.intentos, 1) AS Intentos, COALESCE(e.etapa, 'envio') AS Etapa
            FROM productos p
            JOIN marcas m ON m.id = p.marca_id
            LEFT JOIN categorias c ON c.id = p.categoria_id
            LEFT JOIN LATERAL (
                SELECT x.mensaje_id, x.ultimo_at, x.desde, x.intentos, x.etapa
                FROM envio_errores x
                WHERE x.producto_id = p.id AND x.target = '{EnvioSql.Destino(target)}' AND x.resuelto_at IS NULL
                ORDER BY x.ultimo_at DESC LIMIT 1) e ON TRUE
            LEFT JOIN error_mensajes em ON em.id = e.mensaje_id
            WHERE p.{status} = 2 {texto}
            ORDER BY COALESCE(e.ultimo_at, p.updated_at) DESC, p.code
            LIMIT {MaxFilas}";

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var filas = (await connection.QueryAsync<Fila>(query, parametros)).ToList();
        foreach (var fila in filas)
            fila.Tipo = ErrorClasificador.Clasificar(fila.Mensaje);

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
            Etapa = f.Etapa,
            Intentos = f.Intentos,
            Desde = f.Desde,
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
        string status = StatusCol(target);
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
            SET {status} = 0, updated_at = now()
            WHERE {status} = 2 AND code = ANY(@codes)";

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return await connection.ExecuteAsync(update, new { codes = aReiniciar });
    }

    public async Task<List<ErrorHistorialDto>> HistorialProductoAsync(string code, CancellationToken cancellationToken = default)
    {
        const string query = @"
            SELECT e.target AS Target, e.etapa AS Etapa, em.mensaje AS Mensaje,
                   e.desde AS Desde, e.ultimo_at AS UltimoAt, e.intentos AS Intentos, e.resuelto_at AS ResueltoAt
            FROM envio_errores e
            JOIN productos p ON p.id = e.producto_id
            JOIN error_mensajes em ON em.id = e.mensaje_id
            WHERE p.code = @code
            ORDER BY e.desde DESC, e.id DESC";

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var lista = (await connection.QueryAsync<ErrorHistorialDto>(query, new { code })).ToList();
        foreach (var e in lista)
        {
            var tipo = ErrorClasificador.Clasificar(e.Mensaje);
            e.Tipo = tipo.Codigo;
            e.TipoTitulo = tipo.Titulo;
        }

        return lista;
    }
}

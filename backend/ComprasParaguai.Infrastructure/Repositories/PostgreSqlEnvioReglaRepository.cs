using ComprasParaguai.Core.Interfaces;
using ComprasParaguai.Core.Models;
using Dapper;
using Npgsql;

namespace ComprasParaguai.Infrastructure.Repositories;

/// <summary>
/// Pausas y excepciones de envío en PostgreSQL. Las escrituras corren en una transacción que fija el usuario
/// (app.usuario) para que los triggers de envio_reglas_historial sepan quién hizo cada cambio.
/// </summary>
public class PostgreSqlEnvioReglaRepository : IEnvioReglaRepository
{
    private readonly string _connectionString;

    public PostgreSqlEnvioReglaRepository(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    private static string Destino(SyncTarget target) => EnvioSql.Destino(target);

    private static (string Status, string SentAt) Cols(SyncTarget target) => target switch
    {
        SyncTarget.ComprasParaguai => ("sync_status", "last_sent_at"),
        SyncTarget.ComparaParaguai => ("sync_compara_status", "last_sent_compara_at"),
        _ => throw new ArgumentOutOfRangeException(nameof(target), target, null)
    };

    // ------------------------------------------------------------------ pausas

    private const string PausaSelect = @"
        SELECT pa.id AS Id, pa.target AS Target, pa.marca_id AS MarcaId, m.nombre AS Marca,
               pa.categoria_id AS CategoriaId, c.ruta AS CategoriaRuta,
               pa.retirar_del_destino AS RetirarDelDestino, pa.activo AS Activo, pa.motivo AS Motivo,
               pa.created_by AS CreatedBy, pa.updated_by AS UpdatedBy,
               pa.created_at AS CreatedAt, pa.updated_at AS UpdatedAt,
               (SELECT COUNT(*)
                FROM productos p LEFT JOIN categorias pc ON pc.id = p.categoria_id
                WHERE (pa.marca_id IS NULL OR p.marca_id = pa.marca_id)
                  AND (pa.categoria_id IS NULL OR pc.ruta = c.ruta OR left(pc.ruta, length(c.ruta) + 1) = c.ruta || '>')) AS Productos
        FROM envio_pausas pa
        LEFT JOIN marcas m ON m.id = pa.marca_id
        LEFT JOIN categorias c ON c.id = pa.categoria_id";

    public async Task<List<EnvioPausaDto>> ListarPausasAsync(SyncTarget target, CancellationToken cancellationToken = default)
    {
        string query = $"{PausaSelect} WHERE pa.target = @target ORDER BY pa.activo DESC, pa.created_at DESC";

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return (await connection.QueryAsync<EnvioPausaDto>(query, new { target = Destino(target) })).ToList();
    }

    public async Task<EnvioImpactoDto> ImpactoAsync(SyncTarget target, int? marcaId, int? categoriaId, CancellationToken cancellationToken = default)
    {
        var cols = Cols(target);
        string query = $@"
            SELECT COUNT(*)::int AS Productos,
                   (COUNT(*) FILTER (WHERE p.{cols.Status} = 0))::int AS Pendientes,
                   (COUNT(*) FILTER (WHERE p.{cols.SentAt} IS NOT NULL))::int AS Publicados
            FROM productos p
            LEFT JOIN categorias c ON c.id = p.categoria_id
            WHERE (@marcaId::int IS NULL OR p.marca_id = @marcaId::int)
              AND (@categoriaId::int IS NULL OR EXISTS (
                    SELECT 1 FROM categorias raiz
                    WHERE raiz.id = @categoriaId::int
                      AND (c.ruta = raiz.ruta OR left(c.ruta, length(raiz.ruta) + 1) = raiz.ruta || '>')))";

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return await connection.QuerySingleAsync<EnvioImpactoDto>(query, new { marcaId, categoriaId });
    }

    public async Task<EnvioPausaDto> CrearPausaAsync(CrearPausaRequest request, CambioContexto contexto, CancellationToken cancellationToken = default)
    {
        const string upsert = @"
            INSERT INTO envio_pausas (target, marca_id, categoria_id, retirar_del_destino, motivo, created_by, updated_by)
            VALUES (@target, @marcaId, @categoriaId, @retirar, @motivo, @usuario, @usuario)
            ON CONFLICT (target, COALESCE(marca_id, 0), COALESCE(categoria_id, 0)) DO UPDATE SET
                activo = TRUE,
                retirar_del_destino = EXCLUDED.retirar_del_destino,
                motivo = COALESCE(EXCLUDED.motivo, envio_pausas.motivo),
                updated_by = EXCLUDED.updated_by,
                updated_at = now()
            RETURNING id";

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken);
        await AuditoriaSesion.ConfigurarAsync(connection, tx, contexto, cancellationToken);

        int id = await connection.ExecuteScalarAsync<int>(upsert, new
        {
            target = Destino(request.Target),
            marcaId = request.MarcaId,
            categoriaId = request.CategoriaId,
            retirar = request.RetirarDelDestino,
            motivo = string.IsNullOrWhiteSpace(request.Motivo) ? null : request.Motivo.Trim(),
            usuario = contexto.Usuario
        }, tx);
        await tx.CommitAsync(cancellationToken);

        return await connection.QuerySingleAsync<EnvioPausaDto>($"{PausaSelect} WHERE pa.id = @id", new { id });
    }

    public async Task<bool> ActualizarPausaAsync(int id, bool? activo, string? motivo, bool? retirarDelDestino, CambioContexto contexto, CancellationToken cancellationToken = default)
    {
        const string query = @"
            UPDATE envio_pausas SET
                activo = COALESCE(@activo, activo),
                motivo = COALESCE(@motivo, motivo),
                retirar_del_destino = COALESCE(@retirar, retirar_del_destino),
                updated_by = @usuario,
                updated_at = now()
            WHERE id = @id";

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken);
        await AuditoriaSesion.ConfigurarAsync(connection, tx, contexto, cancellationToken);
        int n = await connection.ExecuteAsync(query, new { id, activo, motivo, retirar = retirarDelDestino, usuario = contexto.Usuario }, tx);
        await tx.CommitAsync(cancellationToken);
        return n > 0;
    }

    public async Task<bool> BorrarPausaAsync(int id, CambioContexto contexto, CancellationToken cancellationToken = default)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken);
        await AuditoriaSesion.ConfigurarAsync(connection, tx, contexto, cancellationToken);
        int n = await connection.ExecuteAsync("DELETE FROM envio_pausas WHERE id = @id", new { id }, tx);
        await tx.CommitAsync(cancellationToken);
        return n > 0;
    }

    // ------------------------------------------------------------- excepciones

    public async Task<List<EnvioEstadoProductoDto>?> EstadoProductoAsync(string code, CancellationToken cancellationToken = default)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        int? productoId = await connection.ExecuteScalarAsync<int?>("SELECT id FROM productos WHERE code = @code", new { code });
        if (productoId is null)
            return null;

        var resultado = new List<EnvioEstadoProductoDto>();
        foreach (var target in new[] { SyncTarget.ComprasParaguai, SyncTarget.ComparaParaguai })
        {
            string t = Destino(target);
            string query = $@"
                SELECT {EnvioSql.RetenidoPor(target)} AS RetenidoPor,
                       EXISTS (SELECT 1 FROM envio_retirados r WHERE r.producto_id = p.id AND r.target = '{t}') AS Retirado
                FROM productos p LEFT JOIN categorias c ON c.id = p.categoria_id
                WHERE p.id = @productoId";
            var fila = await connection.QuerySingleAsync<(string? RetenidoPor, bool Retirado)>(query, new { productoId });

            var excepcion = await connection.QueryFirstOrDefaultAsync<EnvioExcepcionDto>(@"
                SELECT accion AS Accion, activo AS Activo, motivo AS Motivo, updated_by AS UpdatedBy, updated_at AS UpdatedAt
                FROM envio_excepciones WHERE producto_id = @productoId AND target = @target",
                new { productoId, target = t });

            resultado.Add(new EnvioEstadoProductoDto
            {
                Target = t,
                Excepcion = excepcion,
                Retenido = fila.RetenidoPor is not null,
                RetenidoPor = fila.RetenidoPor,
                Retirado = fila.Retirado
            });
        }

        return resultado;
    }

    public async Task<bool> GuardarExcepcionAsync(string code, SyncTarget target, string accion, string? motivo, CambioContexto contexto, CancellationToken cancellationToken = default)
    {
        const string upsert = @"
            INSERT INTO envio_excepciones (target, producto_id, accion, motivo, created_by, updated_by)
            SELECT @target, id, @accion, @motivo, @usuario, @usuario FROM productos WHERE code = @code
            ON CONFLICT (target, producto_id) DO UPDATE SET
                accion = EXCLUDED.accion,
                activo = TRUE,
                motivo = EXCLUDED.motivo,
                updated_by = EXCLUDED.updated_by,
                updated_at = now()";

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken);
        await AuditoriaSesion.ConfigurarAsync(connection, tx, contexto, cancellationToken);
        int n = await connection.ExecuteAsync(upsert, new
        {
            code,
            target = Destino(target),
            accion,
            motivo = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim(),
            usuario = contexto.Usuario
        }, tx);
        await tx.CommitAsync(cancellationToken);
        return n > 0;
    }

    public async Task<bool> BorrarExcepcionAsync(string code, SyncTarget target, CambioContexto contexto, CancellationToken cancellationToken = default)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken);
        await AuditoriaSesion.ConfigurarAsync(connection, tx, contexto, cancellationToken);
        int n = await connection.ExecuteAsync(
            "DELETE FROM envio_excepciones WHERE target = @target AND producto_id = (SELECT id FROM productos WHERE code = @code)",
            new { code, target = Destino(target) }, tx);
        await tx.CommitAsync(cancellationToken);
        return n > 0;
    }

    // --------------------------------------------------------------- historial

    public async Task<ReglaHistorialListDto> HistorialAsync(SyncTarget? target, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        // El destino está dentro del JSON de la regla (despues, o antes si se borró).
        string where = target.HasValue ? "WHERE COALESCE(despues, antes)->>'target' = @target" : "";

        string query = $@"
            SELECT id AS Id, tabla AS Tabla, regla_id AS ReglaId, accion AS Accion, detalle AS Detalle,
                   usuario AS Usuario, changed_at AS ChangedAt
            FROM envio_reglas_historial
            {where}
            ORDER BY changed_at DESC, id DESC
            LIMIT @pageSize OFFSET @offset";
        string countQuery = $"SELECT COUNT(*) FROM envio_reglas_historial {where}";

        var parameters = new
        {
            target = target.HasValue ? Destino(target.Value) : null,
            pageSize,
            offset = (page - 1) * pageSize
        };

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var items = (await connection.QueryAsync<ReglaHistorialDto>(query, parameters)).ToList();
        int total = await connection.ExecuteScalarAsync<int>(countQuery, parameters);

        return new ReglaHistorialListDto
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize)
        };
    }

    // ---------------------------------------------------------- acciones masivas y lista de retenidos

    public async Task<int> AplicarExcepcionLoteAsync(IReadOnlyCollection<SyncTarget> targets, ProductosFiltro filtro, IReadOnlyCollection<string>? codes,
        string? accion, string? motivo, CambioContexto contexto, CancellationToken cancellationToken = default)
    {
        var (conditions, parameters) = PostgreSqlProductoRepository.FiltroProductos(
            filtro.Code, filtro.Name, filtro.SyncStatus, filtro.Target, filtro.MarcaId, filtro.CategoriaId, filtro.Regimen);
        if (codes is { Count: > 0 })
        {
            conditions.Add("p.code = ANY(@codes)");
            parameters.Add("codes", codes.ToArray());
        }
        string where = conditions.Count > 0 ? " AND " + string.Join(" AND ", conditions) : "";

        parameters.Add("accion", accion);
        parameters.Add("motivo", string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim());
        parameters.Add("usuario", contexto.Usuario);

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken);
        await AuditoriaSesion.ConfigurarAsync(connection, tx, contexto, cancellationToken);

        int total = 0;
        foreach (var target in targets.Distinct())
        {
            parameters.Add("target", Destino(target));
            if (accion is null)
            {
                total += await connection.ExecuteAsync($@"
                    DELETE FROM envio_excepciones e USING productos p
                    WHERE e.producto_id = p.id AND e.target = @target{where}", parameters, tx);
            }
            else
            {
                // Solo escribe lo que cambia: una excepción igual a la pedida no se reescribe (así no ensucia el historial).
                total += await connection.ExecuteAsync($@"
                    INSERT INTO envio_excepciones (target, producto_id, accion, motivo, created_by, updated_by)
                    SELECT @target, p.id, @accion, @motivo, @usuario, @usuario FROM productos p WHERE TRUE{where}
                    ON CONFLICT (target, producto_id) DO UPDATE SET
                        accion = EXCLUDED.accion, activo = TRUE, motivo = EXCLUDED.motivo,
                        updated_by = EXCLUDED.updated_by, updated_at = now()
                    WHERE envio_excepciones.accion IS DISTINCT FROM EXCLUDED.accion
                       OR NOT envio_excepciones.activo
                       OR envio_excepciones.motivo IS DISTINCT FROM EXCLUDED.motivo", parameters, tx);
            }
        }

        await tx.CommitAsync(cancellationToken);
        return total;
    }

    public async Task<RetenidosListDto> BuscarRetenidosAsync(SyncTarget target, string? origen, string? q, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var estado = Cols(target).Status;
        string retenido = EnvioSql.Retenido(target);
        string retenidoPor = EnvioSql.RetenidoPor(target);
        string porExcepcion = EnvioSql.ExcepcionRetener(target);

        var parameters = new DynamicParameters();
        var baseConditions = new List<string> { retenido };
        if (!string.IsNullOrWhiteSpace(q))
        {
            baseConditions.Add("(p.code ILIKE @q OR p.name_pt ILIKE @q OR p.name_es ILIKE @q OR m.nombre ILIKE @q)");
            parameters.Add("q", $"%{q.Trim()}%");
        }
        string whereBase = "WHERE " + string.Join(" AND ", baseConditions);

        var itemConditions = new List<string>(baseConditions);
        if (string.Equals(origen, "excepcion", StringComparison.OrdinalIgnoreCase)) itemConditions.Add(porExcepcion);
        else if (string.Equals(origen, "pausa", StringComparison.OrdinalIgnoreCase)) itemConditions.Add($"NOT {porExcepcion}");
        string whereItems = "WHERE " + string.Join(" AND ", itemConditions);

        const string from = "productos p JOIN marcas m ON m.id = p.marca_id LEFT JOIN categorias c ON c.id = p.categoria_id";
        parameters.Add("t", Destino(target));
        parameters.Add("pageSize", pageSize);
        parameters.Add("offset", (page - 1) * pageSize);

        string items = $@"
            SELECT p.code AS Code, p.name_pt AS NamePt, m.nombre AS Marca, c.ruta AS CategoriaRuta, p.stock AS Stock,
                   p.{estado} AS Estado, {retenidoPor} AS RetenidoPor, ({porExcepcion}) AS PorExcepcion,
                   (SELECT e.motivo FROM envio_excepciones e
                    WHERE e.producto_id = p.id AND e.target = @t AND e.activo AND e.accion = 'retener') AS Motivo
            FROM {from} {whereItems}
            ORDER BY p.code
            LIMIT @pageSize OFFSET @offset";

        string totalItems = $"SELECT COUNT(*)::int FROM {from} {whereItems}";
        string resumen = $@"
            SELECT (COUNT(*) FILTER (WHERE NOT {porExcepcion}))::int AS PorPausa,
                   (COUNT(*) FILTER (WHERE {porExcepcion}))::int AS PorExcepcion
            FROM {from} {whereBase}";

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var lista = (await connection.QueryAsync<RetenidoDto>(items, parameters)).ToList();
        int total = await connection.ExecuteScalarAsync<int>(totalItems, parameters);
        var (porPausa, porExc) = await connection.QuerySingleAsync<(int PorPausa, int PorExcepcion)>(resumen, parameters);

        return new RetenidosListDto
        {
            Items = lista,
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize),
            PorPausa = porPausa,
            PorExcepcion = porExc
        };
    }

    // ---------------------------------------------------------- marca/categoría

    public async Task<List<MarcaCategoriaDto>> CategoriasDeMarcaAsync(int marcaId, SyncTarget target, CancellationToken cancellationToken = default)
    {
        const string query = @"
            WITH hojas AS (
                SELECT categoria_id, COUNT(*) AS n
                FROM productos
                WHERE marca_id = @marcaId AND categoria_id IS NOT NULL
                GROUP BY categoria_id
            ), nodos AS (
                SELECT n.id, n.nombre, n.nivel, n.ruta, SUM(h.n)::int AS productos
                FROM hojas h
                JOIN categorias l ON l.id = h.categoria_id
                JOIN categorias n ON (l.ruta = n.ruta OR left(l.ruta, length(n.ruta) + 1) = n.ruta || '>')
                WHERE n.nivel BETWEEN 2 AND 3
                GROUP BY n.id, n.nombre, n.nivel, n.ruta
            )
            SELECT nodos.id AS Id, nodos.nombre AS Nombre, nodos.nivel AS Nivel, nodos.ruta AS Ruta, nodos.productos AS Productos,
                   (SELECT pa.id FROM envio_pausas pa
                    WHERE pa.target = @target AND pa.activo AND pa.marca_id = @marcaId AND pa.categoria_id = nodos.id) AS PausaId
            FROM nodos
            ORDER BY nodos.ruta";

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return (await connection.QueryAsync<MarcaCategoriaDto>(query, new { marcaId, target = Destino(target) })).ToList();
    }
}

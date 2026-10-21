using ComprasParaguai.Core.Interfaces;
using ComprasParaguai.Core.Models;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using static ComprasParaguai.Core.Interfaces.IProductoRepository;

namespace ComprasParaguai.Infrastructure.Repositories;

/// <summary>
/// Implementación de IProductoRepository usando PostgreSQL + Npgsql.
/// Usa COPY a tabla staging + INSERT ... ON CONFLICT para upsert masivo.
/// </summary>
public class PostgreSqlProductoRepository : IProductoRepository
{
    private readonly string _connectionString;
    private readonly ILogger<PostgreSqlProductoRepository> _logger;

    public PostgreSqlProductoRepository(string connectionString, ILogger<PostgreSqlProductoRepository> logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Carga todos los (code, source_hash) de la BD en una sola query.
    /// Retorna Dictionary para O(1) lookup en comparación.
    /// </summary>
    public async Task<Dictionary<string, string?>> LoadProductHashMapAsync(CancellationToken cancellationToken = default)
    {
        const string query = "SELECT code, source_hash FROM productos ORDER BY code";

        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync(cancellationToken);
            var result = await connection.QueryAsync<(string code, string? hash)>(query);
            var map = result.ToDictionary(x => x.code, x => x.hash);

            _logger.LogInformation("Cargados {Count} productos de la BD (hash map)", map.Count);
            return map;
        }
    }

    /// <summary>
    /// Upsert de ProductoApi usando COPY a staging + INSERT ... ON CONFLICT.
    /// Más rápido que fila por fila.
    /// Marca todos como Pendiente (0) en sync_status y sync_compara_status.
    /// </summary>
    public async Task UpsertProductsAsync(IEnumerable<ProductoApi> products, DateTime now, CambioContexto contexto, CancellationToken cancellationToken = default)
    {
        var productList = products.ToList();
        if (!productList.Any())
        {
            _logger.LogInformation("No hay productos para upsert");
            return;
        }

        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync(cancellationToken);

            // Para loguear cuántas categorías/marcas nuevas aprendió esta corrida.
            await using var tx = await connection.BeginTransactionAsync(cancellationToken);
            await AuditoriaSesion.ConfigurarAsync(connection, tx, contexto, cancellationToken);

            int categoriasAntes = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM categorias", transaction: tx);
            int marcasAntes = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM marcas", transaction: tx);

            // Crear tabla staging temporal.
            const string createStaging = @"
                DROP TABLE IF EXISTS productos_staging;
                CREATE TEMP TABLE productos_staging (
                    code VARCHAR(255) NOT NULL,
                    name_es VARCHAR(255),
                    name_pt VARCHAR(255) NOT NULL,
                    marca_nombre TEXT,
                    description_es TEXT,
                    description_pt TEXT,
                    categoria_ruta TEXT,
                    regimen_turismo BOOLEAN,
                    price NUMERIC(12,2),
                    precio_exenta NUMERIC(12,2),
                    price_iva NUMERIC(12,2),
                    stock INT NOT NULL,
                    url_image VARCHAR(1000),
                    link VARCHAR(1000),
                    link_purchase VARCHAR(1000),
                    force_image_update BOOLEAN,
                    source_hash CHAR(64),
                    categoria_id INT,
                    marca_id INT
                );
            ";

            await connection.ExecuteAsync(createStaging, transaction: tx);
            _logger.LogInformation("Tabla staging creada");

            // COPY: escribir filas a staging en bulk.
            using (var writer = await connection.BeginBinaryImportAsync(
                "COPY productos_staging (code, name_es, name_pt, marca_nombre, description_es, description_pt, categoria_ruta, regimen_turismo, price, precio_exenta, price_iva, stock, url_image, link, link_purchase, force_image_update, source_hash) FROM STDIN (FORMAT BINARY)"))
            {
                foreach (var product in productList)
                {
                    await writer.StartRowAsync();
                    await writer.WriteAsync(product.Code, NpgsqlDbType.Varchar);
                    await writer.WriteAsync(product.NameEs, NpgsqlDbType.Varchar);
                    await writer.WriteAsync(product.NamePt, NpgsqlDbType.Varchar);
                    await writer.WriteAsync(product.Brand, NpgsqlDbType.Text);
                    await writer.WriteAsync(product.DescriptionEs, NpgsqlDbType.Text);
                    await writer.WriteAsync(product.DescriptionPt, NpgsqlDbType.Text);
                    await writer.WriteAsync(product.Categoria, NpgsqlDbType.Text);
                    await writer.WriteAsync(product.RegimenTurismo, NpgsqlDbType.Boolean);
                    await writer.WriteAsync(product.Price, NpgsqlDbType.Numeric);
                    await writer.WriteAsync(product.PrecioExenta, NpgsqlDbType.Numeric);
                    await writer.WriteAsync(product.PriceIva, NpgsqlDbType.Numeric);
                    await writer.WriteAsync(product.Stock, NpgsqlDbType.Integer);
                    await writer.WriteAsync(product.UrlImage, NpgsqlDbType.Varchar);
                    await writer.WriteAsync(product.Link, NpgsqlDbType.Varchar);
                    await writer.WriteAsync(product.LinkPurchase, NpgsqlDbType.Varchar);
                    await writer.WriteAsync(product.ForceImageUpdate, NpgsqlDbType.Boolean);
                    await writer.WriteAsync(product.SourceHash, NpgsqlDbType.Char);
                }

                await writer.CompleteAsync();
            }

            _logger.LogInformation("COPY completado: {Count} filas en staging", productList.Count);

            // Resolver categoría y marca de cada fila a su id, todo por conjuntos (sin bucles desde C#).
            // Categorías: cada texto crudo de Oracle se normaliza UNA sola vez y queda en categoria_alias;
            // en una corrida normal todo se resuelve con ese join. Solo las rutas nuevas crean nodos.
            const string resolveCategorias = @"
                INSERT INTO categoria_alias (ruta_origen, categoria_id)
                SELECT d.r, fn_asegurar_categoria(fn_normalizar_ruta_categoria(d.r))
                FROM (
                    SELECT DISTINCT s.categoria_ruta AS r
                    FROM productos_staging s
                    WHERE s.categoria_ruta IS NOT NULL
                      AND NOT EXISTS (SELECT 1 FROM categoria_alias a WHERE a.ruta_origen = s.categoria_ruta)
                ) d
                WHERE fn_normalizar_ruta_categoria(d.r) IS NOT NULL
                ON CONFLICT (ruta_origen) DO NOTHING;

                UPDATE productos_staging s
                SET categoria_id = a.categoria_id
                FROM categoria_alias a
                WHERE a.ruta_origen = s.categoria_ruta;
            ";
            await connection.ExecuteAsync(resolveCategorias, transaction: tx);

            // Marcas: las nuevas se agregan a la tabla; vacío/NULL = BRANDLESS.
            const string resolveMarcas = @"
                INSERT INTO marcas (nombre)
                SELECT DISTINCT ON (upper(t.n)) t.n
                FROM (SELECT fn_normalizar_marca(marca_nombre) AS n FROM productos_staging GROUP BY 1) t
                WHERE NOT EXISTS (SELECT 1 FROM marcas m WHERE upper(m.nombre) = upper(t.n))
                ORDER BY upper(t.n), (t.n = upper(t.n)) DESC, t.n
                ON CONFLICT DO NOTHING;

                UPDATE productos_staging s
                SET marca_id = m.id
                FROM marcas m
                WHERE upper(m.nombre) = upper(fn_normalizar_marca(s.marca_nombre));
            ";
            await connection.ExecuteAsync(resolveMarcas, transaction: tx);

            int categoriasDespues = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM categorias", transaction: tx);
            int marcasDespues = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM marcas", transaction: tx);
            _logger.LogInformation("Categorías nuevas: {Categorias}, marcas nuevas: {Marcas}",
                categoriasDespues - categoriasAntes, marcasDespues - marcasAntes);

            // Upsert: INSERT ... ON CONFLICT DO UPDATE con categoria_id y marca_id ya resueltos.
            const string upsert = @"
                INSERT INTO productos (
                    code, name_es, name_pt, marca_id, description_es, description_pt, categoria_id,
                    regimen_turismo, price, precio_exenta, price_iva, stock,
                    url_image, link, link_purchase, force_image_update, source_hash,
                    sync_status, sync_compara_status, last_changed_at, updated_at
                )
                SELECT
                    ps.code, ps.name_es, ps.name_pt, ps.marca_id, ps.description_es, ps.description_pt, ps.categoria_id,
                    ps.regimen_turismo, ps.price, ps.precio_exenta, ps.price_iva, ps.stock,
                    ps.url_image, ps.link, ps.link_purchase, ps.force_image_update, ps.source_hash,
                    0 AS sync_status,
                    0 AS sync_compara_status,
                    @now AS last_changed_at,
                    @now AS updated_at
                FROM productos_staging ps
                ON CONFLICT (code) DO UPDATE SET
                    name_es = EXCLUDED.name_es,
                    name_pt = EXCLUDED.name_pt,
                    marca_id = EXCLUDED.marca_id,
                    description_es = EXCLUDED.description_es,
                    description_pt = EXCLUDED.description_pt,
                    categoria_id = EXCLUDED.categoria_id,
                    regimen_turismo = EXCLUDED.regimen_turismo,
                    price = EXCLUDED.price,
                    precio_exenta = EXCLUDED.precio_exenta,
                    price_iva = EXCLUDED.price_iva,
                    stock = EXCLUDED.stock,
                    url_image = EXCLUDED.url_image,
                    link = EXCLUDED.link,
                    link_purchase = EXCLUDED.link_purchase,
                    force_image_update = EXCLUDED.force_image_update,
                    source_hash = EXCLUDED.source_hash,
                    sync_status = 0,
                    sync_compara_status = 0,
                    last_changed_at = @now,
                    updated_at = @now;
            ";

            int affected = await connection.ExecuteAsync(upsert, new { now }, transaction: tx);
            await tx.CommitAsync(cancellationToken);
            _logger.LogInformation("Upsert completado: {AffectedRows} filas insertadas/actualizadas", affected);
        }
    }

    /// <summary>
    /// Marca productos como no disponibles (baja detectada en reconciliación).
    /// </summary>
    public async Task MarkAsUnavailableAsync(IEnumerable<string> codes, DateTime now, CambioContexto contexto, CancellationToken cancellationToken = default)
    {
        var codeList = codes.ToList();
        if (!codeList.Any())
            return;

        const string query = @"
            UPDATE productos
            SET stock = 0, sync_status = 0, sync_compara_status = 0, last_changed_at = @now, updated_at = @now
            WHERE code = ANY(@codes)
        ";

        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync(cancellationToken);
            await using var tx = await connection.BeginTransactionAsync(cancellationToken);
            await AuditoriaSesion.ConfigurarAsync(connection, tx, contexto, cancellationToken);
            int affected = await connection.ExecuteAsync(query, new { codes = codeList.ToArray(), now }, transaction: tx);
            await tx.CommitAsync(cancellationToken);
            _logger.LogInformation("Marcados {Count} productos como no disponibles (baja)", affected);
        }
    }

    /// <summary>
    /// Mueve al archivo el historial más viejo que N meses (fn_archivar_historial).
    /// </summary>
    public async Task<long> ArchivarHistorialAsync(int meses, CancellationToken cancellationToken = default)
    {
        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync(cancellationToken);
            long movidas = await connection.ExecuteScalarAsync<long>("SELECT fn_archivar_historial(@meses)", new { meses });
            _logger.LogInformation("Historial: {Movidas} filas con más de {Meses} meses movidas al archivo", movidas, meses);
            return movidas;
        }
    }

    /// <summary>
    /// Recalcula marcas.activo y categorias.activo a partir de los productos existentes.
    /// </summary>
    public async Task RefreshCatalogoActivoAsync(CancellationToken cancellationToken = default)
    {
        const string query = @"
            UPDATE marcas m
            SET activo = EXISTS (SELECT 1 FROM productos p WHERE p.marca_id = m.id)
            WHERE m.activo IS DISTINCT FROM EXISTS (SELECT 1 FROM productos p WHERE p.marca_id = m.id);

            WITH RECURSIVE con_productos AS (
                SELECT DISTINCT categoria_id AS id FROM productos WHERE categoria_id IS NOT NULL
                UNION
                SELECT c.parent_id
                FROM categorias c
                JOIN con_productos cp ON cp.id = c.id
                WHERE c.parent_id IS NOT NULL
            )
            UPDATE categorias c
            SET activo = (c.id IN (SELECT id FROM con_productos))
            WHERE c.activo IS DISTINCT FROM (c.id IN (SELECT id FROM con_productos));
        ";

        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync(cancellationToken);
            await connection.ExecuteAsync(query);
            _logger.LogInformation("Estado activo de marcas y categorías recalculado");
        }
    }

    // Alias: p = productos, m = marcas (nombre -> Brand), c = categorias (ruta -> Categoria).
    private const string ProductoApiColumns = @"
                p.code AS Code,
                p.name_es AS NameEs,
                p.name_pt AS NamePt,
                p.price AS Price,
                p.price_iva AS PriceIva,
                p.regimen_turismo AS RegimenTurismo,
                p.precio_exenta AS PrecioExenta,
                p.stock AS Stock,
                m.nombre AS Brand,
                c.ruta AS Categoria,
                p.description_es AS DescriptionEs,
                p.description_pt AS DescriptionPt,
                p.url_image AS UrlImage,
                p.link AS Link,
                p.link_purchase AS LinkPurchase,
                p.force_image_update AS ForceImageUpdate,
                p.source_hash AS SourceHash,
                p.sync_status AS SyncStatus,
                p.sync_compara_status AS SyncComparaStatus,
                p.last_sent_at AS LastSentAt,
                p.last_sent_compara_at AS LastSentComparaAt,
                p.last_error AS LastError,
                p.last_error_compara AS LastErrorCompara";

    private const string ProductoApiFrom =
        "productos p JOIN marcas m ON m.id = p.marca_id LEFT JOIN categorias c ON c.id = p.categoria_id";

    // Columnas de estado por destino. Switch cerrado: los nombres nunca vienen de input externo.
    private static (string Status, string SentAt, string Error) Cols(SyncTarget target) => target switch
    {
        SyncTarget.ComprasParaguai => ("sync_status", "last_sent_at", "last_error"),
        SyncTarget.ComparaParaguai => ("sync_compara_status", "last_sent_compara_at", "last_error_compara"),
        _ => throw new ArgumentOutOfRangeException(nameof(target), target, null)
    };

    /// <summary>
    /// Carga todos los productos enviables al destino (sin filtrar por estado; sin los retenidos por pausa/excepción).
    /// </summary>
    public async Task<List<ProductoApi>> LoadAllProductsAsync(SyncTarget target, CancellationToken cancellationToken = default)
    {
        string query = $"SELECT {ProductoApiColumns} FROM {ProductoApiFrom} WHERE NOT {EnvioSql.Retenido(target)} ORDER BY p.code";

        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync(cancellationToken);
            var result = (await connection.QueryAsync<ProductoApi>(query)).ToList();

            _logger.LogInformation("Cargados {Count} productos enviables a {Target}", result.Count, target);
            return result;
        }
    }

    /// <summary>
    /// Carga N productos enviables al azar (envíos de prueba).
    /// </summary>
    public async Task<List<ProductoApi>> LoadRandomProductsAsync(int count, SyncTarget target, CancellationToken cancellationToken = default)
    {
        string query = $"SELECT {ProductoApiColumns} FROM {ProductoApiFrom} WHERE NOT {EnvioSql.Retenido(target)} ORDER BY random() LIMIT @count";

        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync(cancellationToken);
            var result = (await connection.QueryAsync<ProductoApi>(query, new { count })).ToList();

            _logger.LogInformation("Cargados {Count} productos al azar para {Target}", result.Count, target);
            return result;
        }
    }

    /// <summary>
    /// Carga N productos al azar marcados como Enviado (status = 1) para el destino.
    /// </summary>
    public async Task<List<ProductoApi>> LoadRandomSentProductsAsync(int count, SyncTarget target, CancellationToken cancellationToken = default)
    {
        var c = Cols(target);
        string query = $"SELECT {ProductoApiColumns} FROM {ProductoApiFrom} WHERE p.{c.Status} = 1 ORDER BY random() LIMIT @count";

        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync(cancellationToken);
            var result = (await connection.QueryAsync<ProductoApi>(query, new { count })).ToList();

            _logger.LogInformation("Cargados {Count} productos Enviados al azar para {Target}", result.Count, target);
            return result;
        }
    }

    /// <summary>
    /// Carga productos Pendiente (status = 0) del destino que se pueden enviar (sin los retenidos).
    /// </summary>
    public async Task<List<ProductoApi>> LoadPendingProductsAsync(SyncTarget target = SyncTarget.ComprasParaguai, CancellationToken cancellationToken = default)
    {
        var c = Cols(target);
        string query = $"SELECT {ProductoApiColumns} FROM {ProductoApiFrom} WHERE p.{c.Status} = 0 AND NOT {EnvioSql.Retenido(target)} ORDER BY p.code";

        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync(cancellationToken);
            var result = (await connection.QueryAsync<ProductoApi>(query)).ToList();

            _logger.LogInformation("Cargados {Count} productos Pendiente enviables para {Target}", result.Count, target);
            return result;
        }
    }

    /// <summary>
    /// Cuenta productos Pendiente (status = 0) enviables del destino (sin los retenidos).
    /// </summary>
    public async Task<int> CountPendingProductsAsync(SyncTarget target = SyncTarget.ComprasParaguai, CancellationToken cancellationToken = default)
    {
        var c = Cols(target);
        string query = $"SELECT COUNT(*) FROM {ProductoApiFrom} WHERE p.{c.Status} = 0 AND NOT {EnvioSql.Retenido(target)}";

        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync(cancellationToken);
            return await connection.ExecuteScalarAsync<int>(query);
        }
    }

    /// <summary>
    /// Todos los pendientes del destino con su estado de retención (para la pestaña Pendientes).
    /// </summary>
    public async Task<List<ProductoPendiente>> LoadPendientesConRetencionAsync(SyncTarget target = SyncTarget.ComprasParaguai, CancellationToken cancellationToken = default)
    {
        var c = Cols(target);
        string query = $@"
            SELECT {ProductoApiColumns},
                   {EnvioSql.RetenidoPor(target)} AS RetenidoPor,
                   ({EnvioSql.RetenidoPor(target)} IS NOT NULL) AS Retenido
            FROM {ProductoApiFrom}
            WHERE p.{c.Status} = 0
            ORDER BY p.code";

        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync(cancellationToken);
            var result = (await connection.QueryAsync<ProductoPendiente>(query)).ToList();

            _logger.LogInformation("Cargados {Count} pendientes de {Target} ({Held} retenidos)", result.Count, target, result.Count(p => p.Retenido));
            return result;
        }
    }

    /// <summary>
    /// Productos publicados en el destino (alguna vez enviados) que una pausa con "retirar del destino" cubre
    /// y que todavía no se retiraron.
    /// </summary>
    public async Task<List<ProductoApi>> LoadParaRetirarAsync(SyncTarget target, CancellationToken cancellationToken = default)
    {
        var c = Cols(target);
        string query = $@"
            SELECT {ProductoApiColumns}
            FROM {ProductoApiFrom}
            WHERE p.{c.SentAt} IS NOT NULL
              AND {EnvioSql.Retirable(target)}
              AND NOT EXISTS (SELECT 1 FROM envio_retirados r WHERE r.producto_id = p.id AND r.target = '{EnvioSql.Destino(target)}')
            ORDER BY p.code";

        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync(cancellationToken);
            var result = (await connection.QueryAsync<ProductoApi>(query)).ToList();

            _logger.LogInformation("{Count} productos para retirar de {Target}", result.Count, target);
            return result;
        }
    }

    /// <summary>
    /// Registra como retirados del destino (se les envió stock 0) los productos indicados.
    /// </summary>
    public async Task MarkRetiradosAsync(IEnumerable<string> codes, SyncTarget target, CancellationToken cancellationToken = default)
    {
        var codeList = codes.ToList();
        if (codeList.Count == 0)
            return;

        string query = $@"
            INSERT INTO envio_retirados (producto_id, target)
            SELECT id, '{EnvioSql.Destino(target)}' FROM productos WHERE code = ANY(@codes)
            ON CONFLICT DO NOTHING";

        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync(cancellationToken);
            int n = await connection.ExecuteAsync(query, new { codes = codeList.ToArray() });
            _logger.LogInformation("Registrados {Count} productos retirados de {Target}", n, target);
        }
    }

    /// <summary>
    /// Los retirados que ya no están retenidos vuelven a Pendiente (se reenvían con su stock real).
    /// </summary>
    public async Task<int> LiberarRetiradosAsync(SyncTarget target, CancellationToken cancellationToken = default)
    {
        var c = Cols(target);
        string query = $@"
            WITH liberados AS (
                DELETE FROM envio_retirados r
                USING productos p LEFT JOIN categorias c ON c.id = p.categoria_id
                WHERE r.producto_id = p.id
                  AND r.target = '{EnvioSql.Destino(target)}'
                  AND NOT {EnvioSql.Retenido(target)}
                RETURNING r.producto_id
            )
            UPDATE productos SET {c.Status} = 0, updated_at = now()
            WHERE id IN (SELECT producto_id FROM liberados)";

        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync(cancellationToken);
            int n = await connection.ExecuteAsync(query);
            if (n > 0)
                _logger.LogInformation("{Count} productos retirados de {Target} se liberan y vuelven a Pendiente", n, target);
            return n;
        }
    }

    /// <summary>
    /// Marca productos como Enviado (status = 1) en la columna del destino.
    /// </summary>
    public async Task MarkAsSentAsync(IEnumerable<string> codes, DateTime now, SyncTarget target = SyncTarget.ComprasParaguai, CancellationToken cancellationToken = default)
    {
        var codeList = codes.ToList();
        if (!codeList.Any())
            return;

        var c = Cols(target);
        string query = $@"
            UPDATE productos
            SET {c.Status} = 1, {c.SentAt} = @now, updated_at = @now
            WHERE code = ANY(@codes)
        ";

        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync(cancellationToken);
            int affected = await connection.ExecuteAsync(query, new { codes = codeList.ToArray(), now });
            _logger.LogInformation("Marcados {Count} productos como Enviado a {Target}", affected, target);
        }
    }

    /// <summary>
    /// Marca un producto como Error (status = 2) en la columna del destino.
    /// </summary>
    public async Task MarkAsErrorAsync(string code, string errorMessage, SyncTarget target = SyncTarget.ComprasParaguai, CancellationToken cancellationToken = default)
    {
        var c = Cols(target);
        string query = $@"
            UPDATE productos
            SET {c.Status} = 2, {c.Error} = @error, updated_at = now()
            WHERE code = @code
        ";

        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync(cancellationToken);
            await connection.ExecuteAsync(query, new { code, error = errorMessage });
            _logger.LogWarning("Producto {Code} marcado como Error en {Target}: {Error}", code, target, errorMessage);
        }
    }

    /// <summary>
    /// Carga productos en Error (status = 2) del destino, con mensaje de error.
    /// </summary>
    public async Task<List<ProductoWithErrorDto>> LoadErrorProductsAsync(SyncTarget target = SyncTarget.ComprasParaguai, CancellationToken cancellationToken = default)
    {
        var c = Cols(target);
        string query = $@"
            SELECT
                code AS Code,
                name_es AS NameEs,
                name_pt AS NamePt,
                {c.Error} AS LastError,
                updated_at AS LastErrorAt
            FROM productos
            WHERE {c.Status} = 2
            ORDER BY updated_at DESC
        ";

        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync(cancellationToken);
            var result = (await connection.QueryAsync<ProductoWithErrorDto>(query)).ToList();

            _logger.LogInformation("Cargados {Count} productos con Error en {Target}", result.Count, target);
            return result;
        }
    }

    /// <summary>
    /// Carga un producto por código (valores actuales).
    /// </summary>
    public async Task<ProductoApi?> LoadProductByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        string query = $"SELECT {ProductoApiColumns} FROM {ProductoApiFrom} WHERE p.code = @code";

        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync(cancellationToken);
            var product = await connection.QueryFirstOrDefaultAsync<ProductoApi>(query, new { code });
            return product;
        }
    }

    /// <summary>
    /// Carga el historial de cambios de un producto.
    /// Retorna lista ordenada por fecha descendente (más reciente primero).
    /// </summary>
    public async Task<List<ProductHistorialDto>> LoadProductHistorialAsync(string code, CancellationToken cancellationToken = default)
    {
        const string query = @"
            SELECT
                changed_at AS ChangedAt,
                name_es AS NameEs,
                name_pt AS NamePt,
                price AS Price,
                price_iva AS PriceIva,
                stock AS Stock,
                brand AS Brand,
                url_image AS UrlImage
            FROM productos_historial
            WHERE code = @code
            ORDER BY changed_at DESC
        ";

        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync(cancellationToken);
            var historial = await connection.QueryAsync<ProductHistorialDto>(query, new { code });
            return historial.ToList();
        }
    }

    /// <summary>
    /// Obtiene el último registro del historial de un producto.
    /// </summary>
    public async Task<ProductHistorialDto?> GetLastHistorialAsync(string code, CancellationToken cancellationToken = default)
    {
        const string query = @"
            SELECT
                changed_at AS ChangedAt,
                name_es AS NameEs,
                name_pt AS NamePt,
                price AS Price,
                price_iva AS PriceIva,
                stock AS Stock,
                brand AS Brand,
                url_image AS UrlImage
            FROM productos_historial
            WHERE code = @code
            ORDER BY changed_at DESC
            LIMIT 1
        ";

        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync(cancellationToken);
            var historial = await connection.QueryFirstOrDefaultAsync<ProductHistorialDto>(query, new { code });
            return historial;
        }
    }

    /// <summary>
    /// Busca y pagina productos en la BD.
    /// </summary>
    public async Task<ProductListDto> SearchProductsAsync(
        string? code, string? name, int? syncStatus, int page, int pageSize, SyncTarget target = SyncTarget.ComprasParaguai,
        int? marcaId = null, int? categoriaId = null, CancellationToken cancellationToken = default)
    {
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        if (!string.IsNullOrEmpty(code))
        {
            conditions.Add("p.code ILIKE @code");
            parameters.Add("code", $"%{code}%");
        }

        if (!string.IsNullOrEmpty(name))
        {
            conditions.Add("(p.name_pt ILIKE @name OR p.name_es ILIKE @name)");
            parameters.Add("name", $"%{name}%");
        }

        if (syncStatus.HasValue)
        {
            conditions.Add($"p.{Cols(target).Status} = @syncStatus");
            parameters.Add("syncStatus", syncStatus.Value);
        }

        if (marcaId.HasValue)
        {
            conditions.Add("p.marca_id = @marcaId");
            parameters.Add("marcaId", marcaId.Value);
        }

        if (categoriaId.HasValue)
        {
            // La categoría incluye todo su subárbol: la propia ruta o cualquiera que cuelgue de ella ("ruta>...").
            conditions.Add(@"p.categoria_id IN (
                    SELECT sub.id
                    FROM categorias raiz
                    JOIN categorias sub ON sub.ruta = raiz.ruta OR left(sub.ruta, length(raiz.ruta) + 1) = raiz.ruta || '>'
                    WHERE raiz.id = @categoriaId)");
            parameters.Add("categoriaId", categoriaId.Value);
        }

        string whereClause = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";

        string query = $@"
            SELECT {ProductoApiColumns}
            FROM {ProductoApiFrom}
            {whereClause}
            ORDER BY p.code
            LIMIT @pageSize OFFSET @offset
        ";

        string countQuery = $@"
            SELECT COUNT(*) FROM productos p {whereClause}
        ";

        var offset = (page - 1) * pageSize;
        parameters.Add("pageSize", pageSize);
        parameters.Add("offset", offset);

        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync(cancellationToken);
            var items = await connection.QueryAsync<ProductoApi>(query, parameters);
            var total = await connection.ExecuteScalarAsync<int>(countQuery, parameters);

            return new ProductListDto
            {
                Items = items.ToList(),
                TotalCount = total,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize)
            };
        }
    }

    /// <summary>
    /// Obtiene los últimos cambios de todos los productos con paginación.
    /// </summary>
    /// <summary>
    /// Contadores por estado para cada destino, en una sola query.
    /// </summary>
    public async Task<List<TargetStatusCounts>> GetStatusCountsAsync(CancellationToken cancellationToken = default)
    {
        // Pendiente = enviable; Held = pendiente pero retenido por pausa/excepción; Retired = ya retirado del destino.
        string retCompras = EnvioSql.Retenido(SyncTarget.ComprasParaguai);
        string retCompara = EnvioSql.Retenido(SyncTarget.ComparaParaguai);

        string query = $@"
            SELECT
                COUNT(*) FILTER (WHERE p.sync_status = 0 AND NOT {retCompras})         AS compras_pending,
                COUNT(*) FILTER (WHERE p.sync_status = 0 AND {retCompras})             AS compras_held,
                COUNT(*) FILTER (WHERE p.sync_status = 1)                              AS compras_sent,
                COUNT(*) FILTER (WHERE p.sync_status = 2)                              AS compras_error,
                MAX(p.last_sent_at)                                                    AS compras_last_sent,
                COUNT(*) FILTER (WHERE p.sync_compara_status = 0 AND NOT {retCompara}) AS compara_pending,
                COUNT(*) FILTER (WHERE p.sync_compara_status = 0 AND {retCompara})     AS compara_held,
                COUNT(*) FILTER (WHERE p.sync_compara_status = 1)                      AS compara_sent,
                COUNT(*) FILTER (WHERE p.sync_compara_status = 2)                      AS compara_error,
                MAX(p.last_sent_compara_at)                                            AS compara_last_sent,
                (SELECT COUNT(*) FROM envio_retirados WHERE target = 'compras')        AS compras_retired,
                (SELECT COUNT(*) FROM envio_retirados WHERE target = 'compara')        AS compara_retired
            FROM productos p
            LEFT JOIN categorias c ON c.id = p.categoria_id";

        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync(cancellationToken);
            var row = await connection.QuerySingleAsync(query);

            return new List<TargetStatusCounts>
            {
                new(SyncTarget.ComprasParaguai, (int)row.compras_pending, (int)row.compras_sent, (int)row.compras_error, (DateTime?)row.compras_last_sent, (int)row.compras_held, (int)row.compras_retired),
                new(SyncTarget.ComparaParaguai, (int)row.compara_pending, (int)row.compara_sent, (int)row.compara_error, (DateTime?)row.compara_last_sent, (int)row.compara_held, (int)row.compara_retired)
            };
        }
    }

    public async Task<ProductChangeListDto> GetRecentChangesAsync(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        const string query = @"
            WITH changes AS (
                SELECT
                    ph.code,
                    p.name_pt,
                    p.name_es,
                    ph.changed_at,
                    ph.price,
                    ph.price_iva,
                    ph.stock,
                    ph.brand,
                    p.price AS current_price,
                    p.price_iva AS current_price_iva,
                    p.stock AS current_stock,
                    m.nombre AS current_brand,
                    p.name_es AS current_name_es,
                    p.name_pt AS current_name_pt,
                    ROW_NUMBER() OVER (ORDER BY ph.changed_at DESC) as rn
                FROM productos_historial ph
                INNER JOIN productos p ON p.code = ph.code
                INNER JOIN marcas m ON m.id = p.marca_id
            ),
            paginated AS (
                SELECT * FROM changes
                WHERE rn > @offset AND rn <= @offset + @pageSize
            )
            SELECT * FROM paginated
            ORDER BY changed_at DESC
        ";

        const string countQuery = @"
            SELECT COUNT(DISTINCT code) FROM productos_historial
        ";

        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync(cancellationToken);
            var offset = (page - 1) * pageSize;
            var results = await connection.QueryAsync<dynamic>(query, new { offset, pageSize });
            var total = await connection.ExecuteScalarAsync<int>(countQuery);

            var changes = new List<ProductChangeDto>();
            foreach (var row in results)
            {
                // Comparar precio
                if (row.price != row.current_price)
                {
                    changes.Add(new ProductChangeDto
                    {
                        Code = row.code,
                        ProductName = row.name_pt ?? row.name_es,
                        Field = "Precio",
                        Before = row.price?.ToString("F2"),
                        After = row.current_price?.ToString("F2"),
                        ChangedAt = row.changed_at
                    });
                }

                // Comparar stock
                if (row.stock != row.current_stock)
                {
                    changes.Add(new ProductChangeDto
                    {
                        Code = row.code,
                        ProductName = row.name_pt ?? row.name_es,
                        Field = "Stock",
                        Before = row.stock?.ToString(),
                        After = row.current_stock?.ToString(),
                        ChangedAt = row.changed_at
                    });
                }

                // Comparar nombre PT
                if (row.name_pt != row.current_name_pt)
                {
                    changes.Add(new ProductChangeDto
                    {
                        Code = row.code,
                        ProductName = row.name_pt ?? row.name_es,
                        Field = "Nombre (PT)",
                        Before = row.name_pt,
                        After = row.current_name_pt,
                        ChangedAt = row.changed_at
                    });
                }

                // Comparar nombre ES
                if (row.name_es != row.current_name_es)
                {
                    changes.Add(new ProductChangeDto
                    {
                        Code = row.code,
                        ProductName = row.name_pt ?? row.name_es,
                        Field = "Nombre (ES)",
                        Before = row.name_es,
                        After = row.current_name_es,
                        ChangedAt = row.changed_at
                    });
                }

                // Comparar marca
                if (row.brand != row.current_brand)
                {
                    changes.Add(new ProductChangeDto
                    {
                        Code = row.code,
                        ProductName = row.name_pt ?? row.name_es,
                        Field = "Marca",
                        Before = row.brand,
                        After = row.current_brand,
                        ChangedAt = row.changed_at
                    });
                }
            }

            return new ProductChangeListDto
            {
                Items = changes.OrderByDescending(c => c.ChangedAt).ToList(),
                TotalCount = total,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize)
            };
        }
    }
}

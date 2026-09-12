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
    public async Task UpsertProductsAsync(IEnumerable<ProductoApi> products, DateTime now, CancellationToken cancellationToken = default)
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

            // Insertar categorías únicas de manera relacional
            var uniqueCategories = productList
                .Select(p => p.Categoria)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct()
                .ToList();

            if (uniqueCategories.Any())
            {
                const string insertCats = "INSERT INTO categorias (name) VALUES (@name) ON CONFLICT (name) DO NOTHING";
                await connection.ExecuteAsync(insertCats, uniqueCategories.Select(c => new { name = c }));
                _logger.LogInformation("Categorías actualizadas/insertadas en la base de datos");
            }

            // Crear tabla staging temporal.
            const string createStaging = @"
                DROP TABLE IF EXISTS productos_staging;
                CREATE TEMP TABLE productos_staging (
                    code VARCHAR(255) NOT NULL,
                    name_es VARCHAR(255),
                    name_pt VARCHAR(255) NOT NULL,
                    price NUMERIC(12,2),
                    price_iva NUMERIC(12,2),
                    stock INT NOT NULL,
                    brand VARCHAR(255),
                    description_es TEXT,
                    description_pt TEXT,
                    url_image VARCHAR(1000),
                    url_image_full VARCHAR(1000),
                    link VARCHAR(1000),
                    link_purchase VARCHAR(1000),
                    force_image_update BOOLEAN,
                    source_hash CHAR(64),
                    categoria_name VARCHAR(255)
                );
            ";

            await connection.ExecuteAsync(createStaging);
            _logger.LogInformation("Tabla staging creada");

            // COPY: escribir filas a staging en bulk.
            using (var writer = await connection.BeginBinaryImportAsync(
                "COPY productos_staging (code, name_es, name_pt, price, price_iva, stock, brand, description_es, description_pt, url_image, link, link_purchase, force_image_update, source_hash, categoria_name) FROM STDIN (FORMAT BINARY)"))
            {
                foreach (var product in productList)
                {
                    await writer.StartRowAsync();
                    await writer.WriteAsync(product.Code, NpgsqlDbType.Varchar);
                    await writer.WriteAsync(product.NameEs, NpgsqlDbType.Varchar);
                    await writer.WriteAsync(product.NamePt, NpgsqlDbType.Varchar);
                    await writer.WriteAsync(product.Price, NpgsqlDbType.Numeric);
                    await writer.WriteAsync(product.PriceIva, NpgsqlDbType.Numeric);
                    await writer.WriteAsync(product.Stock, NpgsqlDbType.Integer);
                    await writer.WriteAsync(product.Brand, NpgsqlDbType.Varchar);
                    await writer.WriteAsync(product.DescriptionEs, NpgsqlDbType.Text);
                    await writer.WriteAsync(product.DescriptionPt, NpgsqlDbType.Text);
                    await writer.WriteAsync(product.UrlImage, NpgsqlDbType.Varchar);
                    await writer.WriteAsync(product.Link, NpgsqlDbType.Varchar);
                    await writer.WriteAsync(product.LinkPurchase, NpgsqlDbType.Varchar);
                    await writer.WriteAsync(product.ForceImageUpdate, NpgsqlDbType.Boolean);
                    await writer.WriteAsync(product.SourceHash, NpgsqlDbType.Char);
                    await writer.WriteAsync(product.Categoria, NpgsqlDbType.Varchar);
                }

                await writer.CompleteAsync();
            }

            _logger.LogInformation("COPY completado: {Count} filas en staging", productList.Count);

            // Upsert: INSERT ... ON CONFLICT DO UPDATE relacionando categoria_id.
            const string upsert = @"
                INSERT INTO productos (
                    code, name_es, name_pt, price, price_iva, stock, brand,
                    description_es, description_pt, url_image, link, link_purchase,
                    force_image_update, source_hash, sync_status, sync_compara_status, last_changed_at, updated_at,
                    categoria_id
                )
                SELECT
                    ps.code, ps.name_es, ps.name_pt, ps.price, ps.price_iva, ps.stock, ps.brand,
                    ps.description_es, ps.description_pt, ps.url_image, ps.link, ps.link_purchase,
                    ps.force_image_update, ps.source_hash,
                    0 AS sync_status,  -- Pendiente (Compras)
                    0 AS sync_compara_status,  -- Pendiente (Compara)
                    @now AS last_changed_at,
                    @now AS updated_at,
                    c.id AS categoria_id
                FROM productos_staging ps
                LEFT JOIN categorias c ON c.name = ps.categoria_name
                ON CONFLICT (code) DO UPDATE SET
                    name_es = EXCLUDED.name_es,
                    name_pt = EXCLUDED.name_pt,
                    price = EXCLUDED.price,
                    price_iva = EXCLUDED.price_iva,
                    stock = EXCLUDED.stock,
                    brand = EXCLUDED.brand,
                    description_es = EXCLUDED.description_es,
                    description_pt = EXCLUDED.description_pt,
                    url_image = EXCLUDED.url_image,
                    link = EXCLUDED.link,
                    link_purchase = EXCLUDED.link_purchase,
                    force_image_update = EXCLUDED.force_image_update,
                    source_hash = EXCLUDED.source_hash,
                    sync_status = 0,  -- Resetear a Pendiente (Compras)
                    sync_compara_status = 0,  -- Resetear a Pendiente (Compara)
                    last_changed_at = @now,
                    updated_at = @now,
                    categoria_id = EXCLUDED.categoria_id;
            ";

            int affected = await connection.ExecuteAsync(upsert, new { now });
            _logger.LogInformation("Upsert completado: {AffectedRows} filas insertadas/actualizadas", affected);
        }
    }

    /// <summary>
    /// Marca productos como no disponibles (baja detectada en reconciliación).
    /// </summary>
    public async Task MarkAsUnavailableAsync(IEnumerable<string> codes, DateTime now, CancellationToken cancellationToken = default)
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
            int affected = await connection.ExecuteAsync(query, new { codes = codeList.ToArray(), now });
            _logger.LogInformation("Marcados {Count} productos como no disponibles (baja)", affected);
        }
    }

    private const string ProductoApiColumns = @"
                code AS Code,
                name_es AS NameEs,
                name_pt AS NamePt,
                price AS Price,
                price_iva AS PriceIva,
                stock AS Stock,
                brand AS Brand,
                description_es AS DescriptionEs,
                description_pt AS DescriptionPt,
                url_image AS UrlImage,
                link AS Link,
                link_purchase AS LinkPurchase,
                force_image_update AS ForceImageUpdate,
                source_hash AS SourceHash,
                sync_status AS SyncStatus";

    // Columnas de estado por destino. Switch cerrado: los nombres nunca vienen de input externo.
    private static (string Status, string SentAt, string Error) Cols(SyncTarget target) => target switch
    {
        SyncTarget.ComprasParaguai => ("sync_status", "last_sent_at", "last_error"),
        SyncTarget.ComparaParaguai => ("sync_compara_status", "last_sent_compara_at", "last_error_compara"),
        _ => throw new ArgumentOutOfRangeException(nameof(target), target, null)
    };

    /// <summary>
    /// Carga TODOS los productos de la BD (sin filtrar por estado).
    /// </summary>
    public async Task<List<ProductoApi>> LoadAllProductsAsync(CancellationToken cancellationToken = default)
    {
        string query = $"SELECT {ProductoApiColumns} FROM productos ORDER BY code";

        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync(cancellationToken);
            var result = (await connection.QueryAsync<ProductoApi>(query)).ToList();

            _logger.LogInformation("Cargados {Count} productos totales", result.Count);
            return result;
        }
    }

    /// <summary>
    /// Carga N productos al azar (envíos de prueba).
    /// </summary>
    public async Task<List<ProductoApi>> LoadRandomProductsAsync(int count, CancellationToken cancellationToken = default)
    {
        string query = $"SELECT {ProductoApiColumns} FROM productos ORDER BY random() LIMIT @count";

        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync(cancellationToken);
            var result = (await connection.QueryAsync<ProductoApi>(query, new { count })).ToList();

            _logger.LogInformation("Cargados {Count} productos al azar", result.Count);
            return result;
        }
    }

    /// <summary>
    /// Carga productos Pendiente (status = 0) del destino.
    /// </summary>
    public async Task<List<ProductoApi>> LoadPendingProductsAsync(SyncTarget target = SyncTarget.ComprasParaguai, CancellationToken cancellationToken = default)
    {
        var c = Cols(target);
        string query = $"SELECT {ProductoApiColumns} FROM productos WHERE {c.Status} = 0 ORDER BY code";

        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync(cancellationToken);
            var result = (await connection.QueryAsync<ProductoApi>(query)).ToList();

            _logger.LogInformation("Cargados {Count} productos Pendiente para {Target}", result.Count, target);
            return result;
        }
    }

    /// <summary>
    /// Cuenta productos Pendiente (status = 0) del destino.
    /// </summary>
    public async Task<int> CountPendingProductsAsync(SyncTarget target = SyncTarget.ComprasParaguai, CancellationToken cancellationToken = default)
    {
        var c = Cols(target);
        string query = $"SELECT COUNT(*) FROM productos WHERE {c.Status} = 0";

        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync(cancellationToken);
            return await connection.ExecuteScalarAsync<int>(query);
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
        const string query = @"
            SELECT
                code AS Code,
                name_es AS NameEs,
                name_pt AS NamePt,
                price AS Price,
                price_iva AS PriceIva,
                stock AS Stock,
                brand AS Brand,
                description_es AS DescriptionEs,
                description_pt AS DescriptionPt,
                url_image AS UrlImage,
                link AS Link,
                link_purchase AS LinkPurchase,
                force_image_update AS ForceImageUpdate,
                source_hash AS SourceHash,
                sync_status AS SyncStatus
            FROM productos
            WHERE code = @code
        ";

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
        string? code, string? name, int? syncStatus, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        if (!string.IsNullOrEmpty(code))
        {
            conditions.Add("code ILIKE @code");
            parameters.Add("code", $"%{code}%");
        }

        if (!string.IsNullOrEmpty(name))
        {
            conditions.Add("(name_pt ILIKE @name OR name_es ILIKE @name)");
            parameters.Add("name", $"%{name}%");
        }

        if (syncStatus.HasValue)
        {
            conditions.Add("sync_status = @syncStatus");
            parameters.Add("syncStatus", syncStatus.Value);
        }

        string whereClause = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";

        string query = $@"
            SELECT
                code AS Code,
                name_es AS NameEs,
                name_pt AS NamePt,
                price AS Price,
                price_iva AS PriceIva,
                stock AS Stock,
                brand AS Brand,
                description_es AS DescriptionEs,
                description_pt AS DescriptionPt,
                url_image AS UrlImage,
                link AS Link,
                link_purchase AS LinkPurchase,
                force_image_update AS ForceImageUpdate,
                source_hash AS SourceHash,
                sync_status AS SyncStatus
            FROM productos
            {whereClause}
            ORDER BY code
            LIMIT @pageSize OFFSET @offset
        ";

        string countQuery = $@"
            SELECT COUNT(*) FROM productos {whereClause}
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
                    p.brand AS current_brand,
                    p.name_es AS current_name_es,
                    p.name_pt AS current_name_pt,
                    ROW_NUMBER() OVER (ORDER BY ph.changed_at DESC) as rn
                FROM productos_historial ph
                INNER JOIN productos p ON p.code = ph.code
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

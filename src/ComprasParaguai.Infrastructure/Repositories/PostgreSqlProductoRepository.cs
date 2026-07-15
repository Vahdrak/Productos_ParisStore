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
    /// Marca todos como sync_status = 0 (Pendiente).
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
                    link VARCHAR(1000),
                    link_purchase VARCHAR(1000),
                    force_image_update BOOLEAN,
                    source_hash CHAR(64)
                );
            ";

            await connection.ExecuteAsync(createStaging);
            _logger.LogInformation("Tabla staging creada");

            // COPY: escribir filas a staging en bulk.
            using (var writer = await connection.BeginBinaryImportAsync(
                "COPY productos_staging (code, name_es, name_pt, price, price_iva, stock, brand, description_es, description_pt, url_image, link, link_purchase, force_image_update, source_hash) FROM STDIN (FORMAT BINARY)"))
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
                }

                await writer.CompleteAsync();
            }

            _logger.LogInformation("COPY completado: {Count} filas en staging", productList.Count);

            // Upsert: INSERT ... ON CONFLICT DO UPDATE.
            const string upsert = @"
                INSERT INTO productos (
                    code, name_es, name_pt, price, price_iva, stock, brand,
                    description_es, description_pt, url_image, link, link_purchase,
                    force_image_update, source_hash, sync_status, last_changed_at, updated_at
                )
                SELECT
                    code, name_es, name_pt, price, price_iva, stock, brand,
                    description_es, description_pt, url_image, link, link_purchase,
                    force_image_update, source_hash,
                    0 AS sync_status,  -- Pendiente
                    @now AS last_changed_at,
                    @now AS updated_at
                FROM productos_staging
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
                    sync_status = 0,  -- Resetear a Pendiente
                    last_changed_at = @now,
                    updated_at = @now;
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
            SET stock = 0, sync_status = 0, last_changed_at = @now, updated_at = @now
            WHERE code = ANY(@codes)
        ";

        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync(cancellationToken);
            int affected = await connection.ExecuteAsync(query, new { codes = codeList.ToArray(), now });
            _logger.LogInformation("Marcados {Count} productos como no disponibles (baja)", affected);
        }
    }

    /// <summary>
    /// Carga productos con sync_status = Pendiente para enviar a la API.
    /// </summary>
    public async Task<List<ProductoApi>> LoadPendingProductsAsync(CancellationToken cancellationToken = default)
    {
        const string query = @"
            SELECT
                code, name_es, name_pt, price, price_iva, stock, brand,
                description_es, description_pt, url_image, link, link_purchase,
                force_image_update, source_hash
            FROM productos
            WHERE sync_status = 0
            ORDER BY code
        ";

        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync(cancellationToken);
            var products = await connection.QueryAsync<ProductoApi>(query);
            var result = products.ToList();

            _logger.LogInformation("Cargados {Count} productos Pendiente", result.Count);
            return result;
        }
    }

    /// <summary>
    /// Marca productos como Enviado (sync_status = 1).
    /// </summary>
    public async Task MarkAsSentAsync(IEnumerable<string> codes, DateTime now, CancellationToken cancellationToken = default)
    {
        var codeList = codes.ToList();
        if (!codeList.Any())
            return;

        const string query = @"
            UPDATE productos
            SET sync_status = 1, last_sent_at = @now, updated_at = @now
            WHERE code = ANY(@codes)
        ";

        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync(cancellationToken);
            int affected = await connection.ExecuteAsync(query, new { codes = codeList.ToArray(), now });
            _logger.LogInformation("Marcados {Count} productos como Enviado", affected);
        }
    }

    /// <summary>
    /// Marca un producto como Error.
    /// </summary>
    public async Task MarkAsErrorAsync(string code, string errorMessage, CancellationToken cancellationToken = default)
    {
        const string query = @"
            UPDATE productos
            SET sync_status = 2, last_error = @error, updated_at = now()
            WHERE code = @code
        ";

        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync(cancellationToken);
            await connection.ExecuteAsync(query, new { code, error = errorMessage });
            _logger.LogWarning("Producto {Code} marcado como Error: {Error}", code, errorMessage);
        }
    }

    /// <summary>
    /// Carga un producto por código (valores actuales).
    /// </summary>
    public async Task<ProductoApi?> LoadProductByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        const string query = @"
            SELECT
                code, name_es, name_pt, price, price_iva, stock, brand,
                description_es, description_pt, url_image, link, link_purchase,
                force_image_update, source_hash
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
}

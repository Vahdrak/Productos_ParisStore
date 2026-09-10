using ComprasParaguai.Core.Models;
using ComprasParaguai.Core.Interfaces;
using ComprasParaguai.Worker.ComparaParaguai.Models;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ComprasParaguai.Worker.ComparaParaguai.Services;

public interface ISyncComparaParaguaiService
{
    Task<int> CheckPendingAsync();
    Task SyncTestBatchAsync(CancellationToken cancellationToken = default);
    Task SyncAllAsync(CancellationToken cancellationToken = default);
}

public class SyncComparaParaguaiService : ISyncComparaParaguaiService
{
    private readonly string _connectionString;
    private readonly HttpClient _httpClient;
    private readonly string _apiUrl;
    private readonly string _apiToken;
    private readonly int _batchSize;
    private readonly ILogger<SyncComparaParaguaiService> _logger;

    public SyncComparaParaguaiService(
        string connectionString,
        string apiUrl,
        string apiToken,
        int batchSize,
        ILogger<SyncComparaParaguaiService> logger)
    {
        _connectionString = connectionString;
        _httpClient = new HttpClient();
        _apiUrl = apiUrl;
        _apiToken = apiToken;
        _batchSize = batchSize;
        _logger = logger;
    }

    public async Task<int> CheckPendingAsync()
    {
        _logger.LogInformation("Verificando productos pendientes...");
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        
        using var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM productos WHERE sync_status = 0", conn);
        var count = (long?)await cmd.ExecuteScalarAsync() ?? 0;
        
        _logger.LogInformation($"✓ Total productos en BD: {count}");
        return (int)count;
    }

    public async Task SyncTestBatchAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("=== MODO PRUEBA ===");
        _logger.LogInformation($"Enviando 500 productos aleatorios a {_apiUrl}");
        
        var productos = await LoadRandomProductosAsync(500, cancellationToken);
        
        if (productos.Count == 0)
        {
            _logger.LogWarning("No hay productos para sincronizar");
            return;
        }

        var batches = DivideBatches(productos, _batchSize);
        await SendBatchesAsync(batches, 1, cancellationToken);
    }

    public async Task SyncAllAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("=== MODO COMPLETO ===");
        _logger.LogInformation($"Enviando TODOS los productos a {_apiUrl}");
        
        var productos = await LoadAllProductosAsync(cancellationToken);
        
        if (productos.Count == 0)
        {
            _logger.LogWarning("No hay productos para sincronizar");
            return;
        }

        _logger.LogInformation($"Total productos a enviar: {productos.Count}");
        var batches = DivideBatches(productos, _batchSize);
        await SendBatchesAsync(batches, 1, cancellationToken);
    }

    private async Task<List<ProductoApi>> LoadRandomProductosAsync(int count, CancellationToken cancellationToken)
    {
        _logger.LogInformation($"Cargando {count} productos aleatorios de la BD...");
        var productos = new List<ProductoApi>();
        
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        
        var sql = "SELECT code, name_es, name_pt, price, price_iva, stock, brand, description_es, description_pt, url_image, link, link_purchase FROM productos ORDER BY RANDOM() LIMIT @count";
        
        using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@count", count);
        
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            productos.Add(MapProducto(reader));
        }
        
        _logger.LogInformation($"✓ Cargados {productos.Count} productos");
        return productos;
    }

    private async Task<List<ProductoApi>> LoadAllProductosAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Cargando todos los productos de la BD...");
        var productos = new List<ProductoApi>();
        
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        
        var sql = "SELECT code, name_es, name_pt, price, price_iva, stock, brand, description_es, description_pt, url_image, link, link_purchase FROM productos WHERE stock > 0 ORDER BY id";
        
        using var cmd = new NpgsqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            productos.Add(MapProducto(reader));
        }
        
        _logger.LogInformation($"✓ Cargados {productos.Count} productos");
        return productos;
    }

    private ProductoApi MapProducto(NpgsqlDataReader reader)
    {
        return new ProductoApi
        {
            Code = reader.GetString(0),
            NameEs = reader.IsDBNull(1) ? "" : reader.GetString(1),
            NamePt = reader.IsDBNull(2) ? "" : reader.GetString(2),
            Price = reader.IsDBNull(3) ? 0 : (decimal)reader.GetValue(3),
            PriceIva = reader.IsDBNull(4) ? null : (decimal)reader.GetValue(4),
            Stock = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
            Brand = reader.IsDBNull(6) ? null : reader.GetString(6),
            DescriptionEs = reader.IsDBNull(7) ? "" : reader.GetString(7),
            DescriptionPt = reader.IsDBNull(8) ? "" : reader.GetString(8),
            UrlImage = reader.IsDBNull(9) ? "" : reader.GetString(9),
            Link = reader.IsDBNull(10) ? "" : reader.GetString(10),
            LinkPurchase = reader.IsDBNull(11) ? null : reader.GetString(11),
        };
    }

    private List<List<ProductoApi>> DivideBatches(List<ProductoApi> productos, int batchSize)
    {
        var batches = new List<List<ProductoApi>>();
        for (int i = 0; i < productos.Count; i += batchSize)
        {
            batches.Add(productos.Skip(i).Take(batchSize).ToList());
        }
        return batches;
    }

    private async Task SendBatchesAsync(List<List<ProductoApi>> batches, int startBatchNum, CancellationToken cancellationToken)
    {
        for (int i = 0; i < batches.Count; i++)
        {
            var batchNum = startBatchNum + i;
            var batch = batches[i];
            await SendBatchAsync(batch, batchNum, cancellationToken);
            
            if (i < batches.Count - 1)
            {
                await Task.Delay(1000, cancellationToken);
            }
        }
    }

    private async Task SendBatchAsync(List<ProductoApi> batch, int batchNum, CancellationToken cancellationToken)
    {
        _logger.LogInformation($"[Lote {batchNum}] Enviando {batch.Count} productos...");
        
        var startTime = DateTime.UtcNow;
        SyncCpRun syncRecord = new()
        {
            StartedAt = startTime,
            BatchNum = batchNum,
            RowsSent = batch.Count
        };

        try
        {
            // Convertir a DTO con snake_case para la API
            var dtos = batch.Select(p => new ProductoComparaParaguaiDto
            {
                Code = p.Code,
                NameEs = p.NameEs,
                NamePt = p.NamePt,
                Price = p.Price,
                PriceIva = p.PriceIva,
                Stock = p.Stock,
                Brand = p.Brand,
                DescriptionEs = p.DescriptionEs,
                DescriptionPt = p.DescriptionPt,
                UrlImage = p.UrlImage,
                Link = p.Link,
                LinkPurchase = p.LinkPurchase
            }).ToList();

            var payload = new { produtos = dtos };
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, _apiUrl)
            {
                Content = content
            };
            request.Headers.Add("Authorization", $"Bearer {_apiToken}");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            
            syncRecord.HttpStatus = (int)response.StatusCode;
            syncRecord.FinishedAt = DateTime.UtcNow;
            syncRecord.Success = response.IsSuccessStatusCode;
            
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
            syncRecord.ResponseText = responseText.Length > 2000 ? responseText.Substring(0, 2000) : responseText;

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"[Lote {batchNum}] ✓ Enviado exitosamente. Status: {response.StatusCode}");
                await MarkProductosAsSentToComparaAsync(batch, cancellationToken);
            }
            else
            {
                syncRecord.ErrorMsg = $"HTTP {response.StatusCode}: {responseText}";
                _logger.LogError($"[Lote {batchNum}] ✗ Error. Status: {response.StatusCode}. Respuesta: {responseText}");
            }
        }
        catch (Exception ex)
        {
            syncRecord.Success = false;
            syncRecord.FinishedAt = DateTime.UtcNow;
            syncRecord.ErrorMsg = ex.Message;
            _logger.LogError($"[Lote {batchNum}] ✗ Excepción: {ex.Message}");
        }

        await RecordSyncRunAsync(syncRecord, cancellationToken);
    }

    private async Task MarkProductosAsSentAsync(List<ProductoApi> batch, CancellationToken cancellationToken)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        
        var codes = string.Join(",", batch.Select(p => $"'{p.Code}'"));
        var sql = $"UPDATE productos SET sync_status = 1 WHERE code IN ({codes})";
        
        using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task MarkProductosAsSentToComparaAsync(List<ProductoApi> batch, CancellationToken cancellationToken)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        var codes = string.Join(",", batch.Select(p => $"'{p.Code}'"));
        var sql = $"UPDATE productos SET sync_compara_status = 1, last_sent_compara_at = NOW() WHERE code IN ({codes})";

        using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task RecordSyncRunAsync(SyncCpRun record, CancellationToken cancellationToken)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        
        var sql = "INSERT INTO sync_cp_runs (started_at, finished_at, batch_num, rows_sent, http_status, response_text, success, error_msg) VALUES (@started, @finished, @batch, @rows, @status, @response, @success, @error)";
        
        using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@started", record.StartedAt);
        cmd.Parameters.AddWithValue("@finished", record.FinishedAt.HasValue ? (object)record.FinishedAt.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@batch", record.BatchNum.HasValue ? (object)record.BatchNum.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@rows", record.RowsSent.HasValue ? (object)record.RowsSent.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@status", record.HttpStatus.HasValue ? (object)record.HttpStatus.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@response", record.ResponseText != null ? (object)record.ResponseText : DBNull.Value);
        cmd.Parameters.AddWithValue("@success", record.Success.HasValue ? (object)record.Success.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@error", record.ErrorMsg != null ? (object)record.ErrorMsg : DBNull.Value);
        
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}

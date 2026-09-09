using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ComprasParaguai.Core.Interfaces;
using ComprasParaguai.Infrastructure;
using ComprasParaguai.Infrastructure.Clients;
using ComprasParaguai.Infrastructure.Repositories;
using ComprasParaguai.Core.Services;
using ComprasParaguai.Worker.ComparaParaguai.Services;

var builder = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json")
    .AddJsonFile("appsettings.Development.json", optional: true);

var config = builder.Build();

var services = new ServiceCollection();
services.AddLogging(options =>
{
    options.AddConsole();
    options.SetMinimumLevel(LogLevel.Information);
});

var connectionString = config["PostgreSQL:ConnectionString"]
    ?? throw new Exception("ConnectionString not found in PostgreSQL:ConnectionString");
var apiUrl = config["ComparaParaguai:ApiUrl"]
    ?? throw new Exception("ComparaParaguai:ApiUrl not found");
var apiToken = config["ComparaParaguai:ApiToken"]
    ?? throw new Exception("ComparaParaguai:ApiToken not found");
var batchSize = config.GetValue("ComparaParaguai:BatchSize", 500);

// Registrar repositorio e cliente de Compras Paraguai
services.AddSingleton<IProductoRepository>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<PostgreSqlProductoRepository>>();
    return new PostgreSqlProductoRepository(connectionString, logger);
});

services.AddSingleton<IComprasParaguaiClient>(sp =>
{
    var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    var logger = sp.GetRequiredService<ILogger<ComprasParaguaiHttpClient>>();
    return new ComprasParaguaiHttpClient(httpClient, apiToken, logger);
});

// Registrar SendService (envío con lotes)
services.AddSingleton<ISendService, SendService>();

// Registrar SyncComparaParaguaiService (legacy, mantiene comandos antiguos)
services.AddSingleton(sp =>
{
    var logger = sp.GetRequiredService<ILogger<SyncComparaParaguaiService>>();
    return new SyncComparaParaguaiService(connectionString, apiUrl, apiToken, batchSize, logger);
});

var serviceProvider = services.BuildServiceProvider();
var syncService = serviceProvider.GetRequiredService<SyncComparaParaguaiService>();
var sendService = serviceProvider.GetRequiredService<ISendService>();
var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

var commandArgs = args ?? Array.Empty<string>();
var command = commandArgs.FirstOrDefault() ?? "--test-batch";

logger.LogInformation("╔═══════════════════════════════════════════╗");
logger.LogInformation("║   ComprasParaguai → Compara Paraguai      ║");
logger.LogInformation("║          Sync Worker v1.0                ║");
logger.LogInformation("╚═══════════════════════════════════════════╝");
logger.LogInformation("");

try
{
    using var cts = new CancellationTokenSource();

    switch (command.ToLower())
    {
        case "--check-pending":
            logger.LogInformation("Comando: --check-pending");
            await syncService.CheckPendingAsync();
            break;

        case "--test-batch":
            logger.LogInformation("Comando: --test-batch (500 productos aleatorios)");
            await syncService.SyncTestBatchAsync(cts.Token);
            break;

        case "--sync-all":
            logger.LogInformation("Comando: --sync-all (todos los productos - legacy)");
            await syncService.SyncAllAsync(cts.Token);
            break;

        case "--send-all":
            logger.LogInformation("Comando: --send-all (ENVÍO FULL: todos los productos en lotes de 500)");
            var resultAll = await sendService.SendAllAsync(cts.Token);
            logger.LogInformation("✓ Envío completado: {Sent} enviados, {Failed} fallidos, {Total} total",
                resultAll.Sent, resultAll.Failed, resultAll.Total);
            break;

        case "--send-pending":
            logger.LogInformation("Comando: --send-pending (ENVÍO INCREMENTAL: solo cambios en lotes de 500)");
            var resultPending = await sendService.SendPendingAsync(cts.Token);
            logger.LogInformation("✓ Envío completado: {Sent} enviados, {Failed} fallidos, {Total} total",
                resultPending.Sent, resultPending.Failed, resultPending.Total);
            break;

        default:
            logger.LogWarning($"Comando no reconocido: {command}");
            logger.LogInformation("Comandos disponibles:");
            logger.LogInformation("  --send-all      Envía TODOS los productos (use una sola vez)");
            logger.LogInformation("  --send-pending  Envía solo cambios (use cada 8 horas)");
            logger.LogInformation("  --check-pending Verifica cuántos pendientes hay");
            logger.LogInformation("  --test-batch    Envía 500 aleatorios (legacy)");
            logger.LogInformation("  --sync-all      Envía todos (legacy)");
            Environment.Exit(1);
            break;
    }
    
    logger.LogInformation("");
    logger.LogInformation("✓ Sincronización completada");
}
catch (Exception ex)
{
    logger.LogError($"✗ Error: {ex.Message}");
    logger.LogError($"Stack: {ex.StackTrace}");
    Environment.Exit(1);
}

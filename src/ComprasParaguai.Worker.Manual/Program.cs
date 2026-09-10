using ComprasParaguai.Core.Interfaces;
using ComprasParaguai.Core.Services;
using ComprasParaguai.Infrastructure;
using ComprasParaguai.Infrastructure.Clients;
using ComprasParaguai.Infrastructure.Readers;
using ComprasParaguai.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

var oracleConnStr = config.GetConnectionString("Oracle") 
    ?? throw new Exception("Oracle connection string not found");
var postgresConnStr = config.GetConnectionString("PostgreSQL")
    ?? throw new Exception("PostgreSQL connection string not found");
var token = config["ComprasParaguai:Token"]
    ?? throw new Exception("ComprasParaguai:Token not found");
var imageBaseUrl = config["Sync:ImageBaseUrl"] ?? "https://default.image.url/";
var imagenPorDefecto = config["Sync:ImagenPorDefecto"] ?? "https://default.image.url/default.png";
var linkPattern = config["Sync:LinkPattern"] ?? "https://default.shop.com/{code}";

Console.WriteLine("╔════════════════════════════════════════╗");
Console.WriteLine("║   ComprasParaguai - Worker Manual      ║");
Console.WriteLine("║          Herramienta de Prueba         ║");
Console.WriteLine("╚════════════════════════════════════════╝");
Console.WriteLine("");

try
{
    Console.WriteLine("Ejecutando migraciones de base de datos...");
    DbMigrator.Migrate(postgresConnStr);
    Console.WriteLine("Migraciones de base de datos finalizadas.");
    Console.WriteLine("");

    services.AddSingleton(new ProductoMapperService(imageBaseUrl, imagenPorDefecto, linkPattern));
    services.AddTransient<ISyncService>(sp =>
        new SyncService(
            sp.GetRequiredService<IViewReader>(),
            sp.GetRequiredService<IProductoRepository>(),
            sp.GetRequiredService<ProductoMapperService>()));
    services.AddTransient<ISendService>(sp =>
        new SendService(
            sp.GetRequiredService<IProductoRepository>(),
            sp.GetRequiredService<IComprasParaguaiClient>()));
    services.AddTransient<IWorkerService>(sp =>
        new WorkerService(
            sp.GetRequiredService<ISyncService>(),
            sp.GetRequiredService<ISendService>(),
            sp.GetRequiredService<ISyncRunRepository>()));

    services.AddTransient<IViewReader>(sp =>
        new OracleViewReader(oracleConnStr, sp.GetRequiredService<ILogger<OracleViewReader>>()));
    services.AddTransient<IProductoRepository>(sp =>
        new PostgreSqlProductoRepository(
            postgresConnStr,
            sp.GetRequiredService<ILogger<PostgreSqlProductoRepository>>()));
    services.AddTransient<ISyncRunRepository>(sp =>
        new PostgreSqlSyncRunRepository(postgresConnStr));

    services.AddTransient<IComprasParaguaiClient>(sp =>
        new ComprasParaguaiHttpClient(
            new HttpClient(),
            token,
            sp.GetRequiredService<ILogger<ComprasParaguaiHttpClient>>()));

    var serviceProvider = services.BuildServiceProvider();
    var workerService = serviceProvider.GetRequiredService<IWorkerService>();

    var command = (args ?? Array.Empty<string>()).FirstOrDefault() ?? "--sync";

    switch (command.ToLower())
    {
        case "--sync":
            Console.WriteLine("Ejecutando sincronización completa...");
            await workerService.ExecuteSyncAsync(CancellationToken.None);
            Console.WriteLine("✓ Sincronización completada");
            break;

        case "--test-batch":
            Console.WriteLine("⚠️  MODO PRUEBA: Enviando 500 productos aleatorios");
            Console.WriteLine("    Los productos se enviarán de VERDAD a la API.");
            Console.WriteLine("");
            Console.WriteLine("Presiona ENTER para continuar, o Ctrl+C para cancelar...");
            Console.ReadLine();
            Console.WriteLine("");
            
            await workerService.ExecuteSyncAsync(CancellationToken.None);
            Console.WriteLine("✓ Prueba completada");
            break;

        default:
            Console.WriteLine($"✗ Comando desconocido: {command}");
            Console.WriteLine("");
            Console.WriteLine("Uso: ComprasParaguai.Worker.Manual [--sync|--test-batch]");
            Console.WriteLine("  --sync       : Ejecutar sincronización completa");
            Console.WriteLine("  --test-batch : Enviar productos (PRUEBA)");
            Environment.Exit(1);
            break;
    }
}
catch (Exception ex)
{
    Console.WriteLine($"✗ Error: {ex.Message}");
    Console.WriteLine($"Stack: {ex.StackTrace}");
    Environment.Exit(1);
}

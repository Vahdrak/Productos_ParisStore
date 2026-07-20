using ComprasParaguai.Core.Interfaces;
using ComprasParaguai.Core.Services;
using ComprasParaguai.Infrastructure;
using ComprasParaguai.Infrastructure.Clients;
using ComprasParaguai.Infrastructure.Readers;
using ComprasParaguai.Infrastructure.Repositories;
using ComprasParaguai.Worker;
using Serilog;

// Configurar Serilog antes del host.
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("=== Iniciando ComprasParaguai Worker ===");

    var builder = Host.CreateApplicationBuilder(args);

    // Serilog.
    builder.Services.AddSerilog();

    // Configuración.
    var config = builder.Configuration;
    var oracleConnStr = config.GetConnectionString("Oracle")
        ?? throw new InvalidOperationException("Oracle connection string no encontrada");
    var postgresConnStr = config.GetConnectionString("PostgreSQL")
        ?? throw new InvalidOperationException("PostgreSQL connection string no encontrada");

    // Ejecutar migraciones automáticas.
    Log.Information("Ejecutando migraciones de base de datos...");
    DbMigrator.Migrate(postgresConnStr);
    Log.Information("Migraciones de base de datos finalizadas.");
    var token = config["ComprasParaguai:Token"]
        ?? throw new InvalidOperationException("Token de Compras Paraguai no configurado");
    var imageBaseUrl = config["Sync:ImageBaseUrl"] ?? "https://default.image.url/";
    var imagenPorDefecto = config["Sync:ImagenPorDefecto"] ?? "https://default.image.url/default.png";
    var linkPattern = config["Sync:LinkPattern"] ?? "https://default.shop.com/{code}";
    var syncIntervalMinutes = config.GetValue<int>("Sync:IntervalMinutes", 15);

    // Core: servicios de lógica.
    builder.Services.AddSingleton(
        new ProductoMapperService(imageBaseUrl, imagenPorDefecto, linkPattern));
    builder.Services.AddTransient<ISyncService>(sp =>
        new SyncService(
            sp.GetRequiredService<IViewReader>(),
            sp.GetRequiredService<IProductoRepository>(),
            sp.GetRequiredService<ProductoMapperService>()));
    builder.Services.AddTransient<ISendService>(sp =>
        new SendService(
            sp.GetRequiredService<IProductoRepository>(),
            sp.GetRequiredService<IComprasParaguaiClient>()));
    builder.Services.AddTransient<IWorkerService>(sp =>
        new WorkerService(
            sp.GetRequiredService<ISyncService>(),
            sp.GetRequiredService<ISendService>(),
            sp.GetRequiredService<ISyncRunRepository>()));

    // Infrastructure: acceso a datos.
    builder.Services.AddTransient<IViewReader>(sp =>
        new OracleViewReader(oracleConnStr, sp.GetRequiredService<ILogger<OracleViewReader>>()));
    builder.Services.AddTransient<IProductoRepository>(sp =>
        new PostgreSqlProductoRepository(postgresConnStr, sp.GetRequiredService<ILogger<PostgreSqlProductoRepository>>()));
    builder.Services.AddTransient<ISyncRunRepository>(sp =>
        new PostgreSqlSyncRunRepository(postgresConnStr));

    // Cliente HTTP.
    builder.Services.AddTransient<IComprasParaguaiClient>(sp =>
        new ComprasParaguaiHttpClient(
            new HttpClient(),
            token,
            sp.GetRequiredService<ILogger<ComprasParaguaiHttpClient>>()));

    // Worker hosted service.
    builder.Services.AddHostedService<SyncWorker>(sp =>
        new SyncWorker(
            sp.GetRequiredService<IWorkerService>(),
            TimeSpan.FromMinutes(syncIntervalMinutes),
            sp.GetRequiredService<ILogger<SyncWorker>>()));

    var host = builder.Build();
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Aplicación terminó inesperadamente");
}
finally
{
    Log.CloseAndFlush();
}

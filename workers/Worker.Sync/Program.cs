using ComprasParaguai.Core.Interfaces;
using ComprasParaguai.Core.Services;
using ComprasParaguai.Infrastructure;
using ComprasParaguai.Infrastructure.Readers;
using ComprasParaguai.Infrastructure.Repositories;
using Serilog;
using Worker.Sync;

// .env junto al ejecutable (ignorado por git); ver .env.example.
DotEnv.Load(Path.Combine(AppContext.BaseDirectory, ".env"));

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("=== Worker.Sync: Oracle → PostgreSQL ===");

    var runOnce = args.Contains("--once", StringComparer.OrdinalIgnoreCase);
    // Content root = carpeta del ejecutable, para que appsettings*.json se encuentren sin importar el cwd.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = args, ContentRootPath = AppContext.BaseDirectory });
    builder.Services.AddSerilog();

    var config = builder.Configuration;
    var oracleConnStr = config.GetConnectionString("Oracle")
        ?? throw new InvalidOperationException("Oracle connection string no encontrada");
    var postgresConnStr = config.GetConnectionString("PostgreSQL")
        ?? throw new InvalidOperationException("PostgreSQL connection string no encontrada");

    Log.Information("Ejecutando migraciones de base de datos...");
    DbMigrator.Migrate(postgresConnStr);

    var imageBaseUrl = config["Sync:ImageBaseUrl"] ?? throw new InvalidOperationException("Sync:ImageBaseUrl no configurada");
    var imagenPorDefecto = config["Sync:ImagenPorDefecto"] ?? throw new InvalidOperationException("Sync:ImagenPorDefecto no configurada");
    var linkPattern = config["Sync:LinkPattern"] ?? throw new InvalidOperationException("Sync:LinkPattern no configurada");
    var syncIntervalMinutes = config.GetValue<int>("Sync:IntervalMinutes", 120);

    builder.Services.AddSingleton(new ProductoMapperService(imageBaseUrl, imagenPorDefecto, linkPattern));
    builder.Services.AddTransient<ISyncService>(sp =>
        new SyncService(
            sp.GetRequiredService<IViewReader>(),
            sp.GetRequiredService<IProductoRepository>(),
            sp.GetRequiredService<ProductoMapperService>()));
    builder.Services.AddTransient<IWorkerService>(sp =>
        new WorkerService(
            sp.GetRequiredService<ISyncService>(),
            sp.GetRequiredService<ISyncRunRepository>()));

    builder.Services.AddTransient<IViewReader>(sp =>
        new OracleViewReader(oracleConnStr, sp.GetRequiredService<ILogger<OracleViewReader>>()));
    builder.Services.AddTransient<IProductoRepository>(sp =>
        new PostgreSqlProductoRepository(postgresConnStr, sp.GetRequiredService<ILogger<PostgreSqlProductoRepository>>()));
    builder.Services.AddTransient<ISyncRunRepository>(sp =>
        new PostgreSqlSyncRunRepository(postgresConnStr));

    if (runOnce)
    {
        Log.Information("Modo --once: un ciclo de sync y salir");
        var host = builder.Build();
        var workerService = host.Services.GetRequiredService<IWorkerService>();
        await workerService.ExecuteSyncAsync();
        await workerService.ExecuteDailyReconciliationAsync();
        Log.Information("✓ Sync completado");
        return;
    }

    builder.Services.AddHostedService<SyncWorker>(sp =>
        new SyncWorker(
            sp.GetRequiredService<IWorkerService>(),
            TimeSpan.FromMinutes(syncIntervalMinutes),
            sp.GetRequiredService<ILogger<SyncWorker>>()));

    await builder.Build().RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Aplicación terminó inesperadamente");
    Environment.ExitCode = 1;
}
finally
{
    Log.CloseAndFlush();
}

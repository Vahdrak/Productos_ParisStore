using ComprasParaguai.Core.Interfaces;
using ComprasParaguai.Core.Models;
using ComprasParaguai.Core.Services;
using ComprasParaguai.Infrastructure;
using ComprasParaguai.Infrastructure.Clients;
using ComprasParaguai.Infrastructure.Hosting;
using ComprasParaguai.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

const string TargetName = "Compara Paraguai";
var options = SendOptions.ComparaParaguai;

// Content root = carpeta del ejecutable, para que appsettings*.json se encuentren sin importar el cwd.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = args, ContentRootPath = AppContext.BaseDirectory });
builder.Logging.ClearProviders().AddSimpleConsole(o => o.TimestampFormat = "HH:mm:ss ");

var config = builder.Configuration;
var connectionString = config.GetConnectionString("PostgreSQL")
    ?? throw new InvalidOperationException("ConnectionStrings:PostgreSQL no configurada");
var apiUrl = config["Compara:ApiUrl"]
    ?? throw new InvalidOperationException("Compara:ApiUrl no configurada");
var token = config["Compara:Token"]
    ?? throw new InvalidOperationException("Compara:Token no configurado (User Secrets / appsettings.Development.json / env)");
var intervalHours = config.GetValue<double>("Compara:IntervalHours", 2);

DbMigrator.Migrate(connectionString);

builder.Services.AddSingleton<IProductoRepository>(sp =>
    new PostgreSqlProductoRepository(connectionString, sp.GetRequiredService<ILogger<PostgreSqlProductoRepository>>()));
builder.Services.AddSingleton<IComprasParaguaiClient>(sp =>
    new ComparaParaguaiHttpClient(new HttpClient(), apiUrl, token, sp.GetRequiredService<ILogger<ComparaParaguaiHttpClient>>()));
builder.Services.AddSingleton<ISendService>(sp =>
    new SendService(sp.GetRequiredService<IProductoRepository>(), sp.GetRequiredService<IComprasParaguaiClient>(), options));

var command = args.FirstOrDefault()?.ToLowerInvariant();

if (command is null)
{
    builder.Services.AddHostedService(sp =>
        new SendWorker(sp.GetRequiredService<ISendService>(), TimeSpan.FromHours(intervalHours), TargetName,
            sp.GetRequiredService<ILogger<SendWorker>>()));
    await builder.Build().RunAsync();
    return;
}

var host = builder.Build();
var logger = host.Services.GetRequiredService<ILogger<Program>>();
var sendService = host.Services.GetRequiredService<ISendService>();
var repository = host.Services.GetRequiredService<IProductoRepository>();

logger.LogInformation("=== Worker.Compara → {Target} ({Url}) ===", TargetName, apiUrl);

try
{
    switch (command)
    {
        case "--send-pending":
            Report(await sendService.SendPendingAsync());
            break;

        case "--send-all":
            logger.LogInformation("Envío COMPLETO: todos los productos en lotes de {Batch}", options.BatchSize);
            Report(await sendService.SendAllAsync());
            break;

        case "--test-batch":
            logger.LogInformation("Prueba: 500 productos al azar (se envían de verdad)");
            Report(await sendService.SendSampleAsync(500));
            break;

        case "--check-pending":
            var pending = await repository.CountPendingProductsAsync(options.Target);
            logger.LogInformation("Pendientes para {Target}: {Count}", TargetName, pending);
            break;

        default:
            logger.LogError("Comando no reconocido: {Command}", command);
            logger.LogInformation("Uso: Worker.Compara [comando]");
            logger.LogInformation("  (sin comando)    Servicio: envía pendientes cada {Hours} h", intervalHours);
            logger.LogInformation("  --send-pending   Envía solo pendientes y sale");
            logger.LogInformation("  --send-all       Envía TODOS los productos (primera vez)");
            logger.LogInformation("  --test-batch     Envía 500 al azar (prueba real)");
            logger.LogInformation("  --check-pending  Cuenta pendientes, no envía");
            Environment.Exit(1);
            break;
    }
}
catch (Exception ex)
{
    logger.LogError(ex, "Error ejecutando {Command}", command);
    Environment.Exit(1);
}

void Report(SendResult r) =>
    logger.LogInformation("✓ {Target}: {Sent} enviados, {Failed} fallidos, {Total} total", TargetName, r.Sent, r.Failed, r.Total);

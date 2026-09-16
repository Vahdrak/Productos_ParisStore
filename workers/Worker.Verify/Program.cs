using System.Globalization;
using System.Text;
using ComprasParaguai.Core.Interfaces;
using ComprasParaguai.Core.Models;
using ComprasParaguai.Core.Services;
using ComprasParaguai.Infrastructure;
using ComprasParaguai.Infrastructure.Clients;
using ComprasParaguai.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// .env junto al ejecutable (ignorado por git); ver .env.example.
DotEnv.Load(Path.Combine(AppContext.BaseDirectory, ".env"));

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = args, ContentRootPath = AppContext.BaseDirectory });
builder.Logging.ClearProviders().AddSimpleConsole(o => o.TimestampFormat = "HH:mm:ss ");

var config = builder.Configuration;
var connectionString = config.GetConnectionString("PostgreSQL")
    ?? throw new InvalidOperationException("ConnectionStrings:PostgreSQL no configurada");
var comprasUrl = config["Compras:ApiUrl"]
    ?? throw new InvalidOperationException("Compras:ApiUrl no configurada");
var comprasToken = config["Compras:Token"]
    ?? throw new InvalidOperationException("Compras:Token no configurado (.env o variable de entorno)");
var defaultSample = config.GetValue<int>("Verify:SampleSize", 500);
var delayMs = config.GetValue<int>("Verify:DelayMs", 100);
var outputDir = Path.GetFullPath(config["Verify:OutputDir"] ?? "verify-output", Directory.GetCurrentDirectory());

builder.Services.AddSingleton<IProductoRepository>(sp =>
    new PostgreSqlProductoRepository(connectionString, sp.GetRequiredService<ILogger<PostgreSqlProductoRepository>>()));
builder.Services.AddSingleton<IProductQueryClient>(sp =>
    new ComprasParaguaiHttpClient(new HttpClient(), comprasUrl, comprasToken, sp.GetRequiredService<ILogger<ComprasParaguaiHttpClient>>()));
builder.Services.AddSingleton<IVerifyRunRepository>(sp =>
    new PostgreSqlVerifyRunRepository(connectionString));

var host = builder.Build();
var logger = host.Services.GetRequiredService<ILogger<Program>>();

var command = args.FirstOrDefault()?.ToLowerInvariant() ?? "--compras";
int sampleSize = args.Length > 1 && int.TryParse(args[1], out var n) && n > 0 ? n : defaultSample;

switch (command)
{
    case "--compras":
        await VerifyAsync(SyncTarget.ComprasParaguai, "Compras Paraguai", host.Services.GetRequiredService<IProductQueryClient>());
        break;

    default:
        logger.LogError("Comando no reconocido: {Command}", command);
        logger.LogInformation("Uso: Worker.Verify [--compras] [cantidad]");
        logger.LogInformation("  --compras [N]   Toma N productos al azar marcados Enviado a Compras (default {N}),", defaultSample);
        logger.LogInformation("                  los busca en la API de Compras y escribe un CSV en {Dir}", outputDir);
        Environment.Exit(1);
        break;
}

async Task VerifyAsync(SyncTarget target, string targetName, IProductQueryClient client)
{
    logger.LogInformation("=== Worker.Verify → {Target}: {N} productos al azar marcados Enviado ===", targetName, sampleSize);

    var service = new VerifyService(
        host.Services.GetRequiredService<IProductoRepository>(),
        client,
        target,
        host.Services.GetRequiredService<IVerifyRunRepository>());
    var progress = new Progress<int>(i => { if (i % 50 == 0) logger.LogInformation("  consultados {I}/{N}", i, sampleSize); });

    var results = await service.VerifyAsync(sampleSize, delayMs, progress);

    if (results.Count == 0)
    {
        logger.LogWarning("No hay productos marcados como Enviado a {Target}", targetName);
        return;
    }

    Directory.CreateDirectory(outputDir);
    var file = Path.Combine(outputDir, $"verify-{target.ToString().ToLowerInvariant()}-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
    await WriteCsvAsync(file, results);

    int found = results.Count(r => r.Found);
    int priceOk = results.Count(r => r.PriceMatch);
    int stockOk = results.Count(r => r.StockMatch);
    var today = DateTime.UtcNow.Date;
    int updatedToday = results.Count(r => r.Remote?.UpdatedAt?.Date == today);

    logger.LogInformation("Resultado {Target}: {Found}/{Total} encontrados en la API ({Pct:P0}). Precio coincide: {Price}. Stock coincide: {Stock}. Actualizados hoy: {Today}.",
        targetName, found, results.Count, (double)found / results.Count, priceOk, stockOk, updatedToday);

    foreach (var missing in results.Where(r => !r.Found).Take(10))
        logger.LogWarning("  NO encontrado: {Code} ({Name})", missing.Local.Code, missing.Local.NamePt);
    if (results.Count(r => !r.Found) > 10)
        logger.LogWarning("  ... ver el CSV para el resto");

    logger.LogInformation("Archivo: {File}", file);
}

static async Task WriteCsvAsync(string path, List<VerifyResult> results)
{
    var sb = new StringBuilder();
    sb.AppendLine("code;encontrado;nombre_local;precio_local;precio_api;precio_ok;stock_local;stock_api;stock_ok;disponible_api;actualizado_api;link_api");
    var inv = CultureInfo.InvariantCulture;

    foreach (var r in results)
    {
        sb.Append(Csv(r.Local.Code)).Append(';')
          .Append(r.Found ? "SI" : "NO").Append(';')
          .Append(Csv(r.Local.NamePt)).Append(';')
          .Append(r.Local.Price.ToString(inv)).Append(';')
          .Append(r.Remote?.Price?.ToString(inv)).Append(';')
          .Append(r.Found ? (r.PriceMatch ? "SI" : "NO") : "").Append(';')
          .Append(r.Local.Stock).Append(';')
          .Append(r.Remote?.Stock?.ToString(inv)).Append(';')
          .Append(r.Found ? (r.StockMatch ? "SI" : "NO") : "").Append(';')
          .Append(r.Remote?.Available switch { true => "SI", false => "NO", _ => "" }).Append(';')
          .Append(r.Remote?.UpdatedAt?.ToString("yyyy-MM-dd HH:mm:ss")).Append(';')
          .Append(Csv(r.Remote?.Link))
          .AppendLine();
    }

    // UTF-8 con BOM para que Excel muestre bien los acentos; separador ';' para Excel en español.
    await File.WriteAllTextAsync(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

    static string Csv(string? s) => s is null ? "" : "\"" + s.Replace("\"", "\"\"") + "\"";
}

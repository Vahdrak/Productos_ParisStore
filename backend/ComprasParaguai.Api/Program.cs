using ComprasParaguai.Infrastructure.Repositories;
using ComprasParaguai.Infrastructure.Readers;
using ComprasParaguai.Infrastructure;
using ComprasParaguai.Core.Interfaces;
using Npgsql;
using Dapper;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

var postgresConnStr = builder.Configuration.GetConnectionString("PostgreSQL")
    ?? builder.Configuration["PostgreSQL:ConnectionString"]
    ?? throw new InvalidOperationException("PostgreSQL connection string not configured");

// Ejecutar migraciones automáticas
try
{
    Console.WriteLine("Ejecutando migraciones de base de datos...");
    DbMigrator.Migrate(postgresConnStr);
    Console.WriteLine("Migraciones de base de datos finalizadas.");
}
catch (Exception ex)
{
    Console.WriteLine($"Error al ejecutar migraciones en la API: {ex.Message}");
}

builder.Services.AddTransient<IProductoRepository>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<PostgreSqlProductoRepository>>();
    return new PostgreSqlProductoRepository(postgresConnStr, logger);
});

builder.Services.AddTransient<ISyncRunRepository>(sp =>
    new PostgreSqlSyncRunRepository(postgresConnStr));

builder.Services.AddTransient<ISendRunRepository>(sp =>
    new PostgreSqlSendRunRepository(postgresConnStr));

builder.Services.AddTransient<IVerifyRunRepository>(sp =>
    new PostgreSqlVerifyRunRepository(postgresConnStr));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAngular");
app.MapControllers();

app.Run();


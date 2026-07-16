using ComprasParaguai.Infrastructure.Repositories;
using ComprasParaguai.Infrastructure.Readers;
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

var postgresConnStr = builder.Configuration["PostgreSQL:ConnectionString"]
    ?? throw new InvalidOperationException("PostgreSQL:ConnectionString not configured");

builder.Services.AddTransient<IProductoRepository>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<PostgreSqlProductoRepository>>();
    return new PostgreSqlProductoRepository(postgresConnStr, logger);
});

builder.Services.AddTransient<ISyncRunRepository>(sp =>
    new PostgreSqlSyncRunRepository(postgresConnStr));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAngular");
app.MapControllers();

app.Run();


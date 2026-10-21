using ComprasParaguai.Api.Auth;
using ComprasParaguai.Core.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
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

// CORS configurado por variable de entorno
var corsOrigins = builder.Configuration["CORS_ALLOWED_ORIGINS"]
    ?? "http://localhost:4200,http://172.16.249.94:4200,http://172.16.249.94";
var origins = corsOrigins.Split(',').Select(o => o.Trim()).ToArray();

Console.WriteLine($"CORS Origins configurados: {string.Join(", ", origins)}");

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins(origins)
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

// ---- Autenticación: JWT para las operaciones de escritura (los GET siguen abiertos) ----
var authOptions = builder.Configuration.GetSection("Auth").Get<AuthOptions>() ?? new AuthOptions();
var jwtKey = AuthService.CrearClave(authOptions.JwtSecret, out var jwtTemporal);
if (jwtTemporal)
    Console.WriteLine("ADVERTENCIA: Auth__JwtSecret no está configurado; se usa una clave temporal (las sesiones se pierden al reiniciar la API).");

builder.Services.AddSingleton(new AuthService(jwtKey, TimeSpan.FromHours(Math.Max(1, authOptions.TokenHoras))));
builder.Services.AddTransient<IUsuarioRepository>(sp => new PostgreSqlUsuarioRepository(postgresConnStr));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;   // se usan los nombres de claim tal cual ("name", "nombre")
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = jwtKey,
            NameClaimType = "name",
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddTransient<ICatalogoRepository>(sp =>
    new PostgreSqlCatalogoRepository(postgresConnStr));

builder.Services.AddTransient<IErrorEnvioRepository>(sp =>
    new PostgreSqlErrorEnvioRepository(postgresConnStr));

builder.Services.AddTransient<IEnvioReglaRepository>(sp =>
    new PostgreSqlEnvioReglaRepository(postgresConnStr));

builder.Services.AddTransient<IHistorialRepository>(sp =>
    new PostgreSqlHistorialRepository(postgresConnStr));

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

// Primer usuario: si la tabla está vacía y hay Auth__AdminUsuario / Auth__AdminPassword, se crea.
try
{
    var adminLogin = builder.Configuration["Auth:AdminUsuario"];
    var adminPassword = builder.Configuration["Auth:AdminPassword"];
    if (!string.IsNullOrWhiteSpace(adminLogin) && !string.IsNullOrEmpty(adminPassword))
    {
        var usuarios = app.Services.GetRequiredService<IUsuarioRepository>();
        if (await usuarios.CountAsync() == 0)
        {
            var admin = new Usuario { Login = adminLogin.Trim(), Nombre = adminLogin.Trim() };
            admin.PasswordHash = app.Services.GetRequiredService<AuthService>().HashPassword(admin, adminPassword);
            await usuarios.CreateAsync(admin);
            Console.WriteLine($"Usuario inicial '{admin.Login}' creado.");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"No se pudo crear el usuario inicial: {ex.Message}");
}

app.UseCors("AllowAngular");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();


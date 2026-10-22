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

// ---- Comando: crear un usuario del panel (o cambiarle la contraseña) y salir, sin levantar el servidor ----
//   dotnet run --project backend/ComprasParaguai.Api -- crear-usuario <usuario> ["Nombre para mostrar"]
//   docker exec -it backend_api_prod dotnet ComprasParaguai.Api.dll crear-usuario <usuario>
// La contraseña se pide por consola (o, para automatizar, de la variable CP_NEW_PASSWORD) y se guarda con hash.
if (args.Length >= 2 && args[0].Equals("crear-usuario", StringComparison.OrdinalIgnoreCase))
{
    Console.OutputEncoding = System.Text.Encoding.UTF8;
    var login = args[1].Trim();
    var nombre = args.Length > 2 ? args[2].Trim() : login;
    var password = Environment.GetEnvironmentVariable("CP_NEW_PASSWORD");
    if (string.IsNullOrEmpty(password))
    {
        password = LeerSecreto("Contraseña (mínimo 8 caracteres): ");
        if (password != LeerSecreto("Repite la contraseña: "))
        {
            Console.WriteLine("Las contraseñas no coinciden. No se hizo nada.");
            return;
        }
    }

    if (login.Length == 0 || password.Length < 8)
    {
        Console.WriteLine("Usuario vacío o contraseña de menos de 8 caracteres. No se hizo nada.");
        return;
    }

    var usuarios = app.Services.GetRequiredService<IUsuarioRepository>();
    var auth = app.Services.GetRequiredService<AuthService>();
    var existente = await usuarios.GetByLoginAsync(login);
    if (existente is null)
    {
        var nuevo = new Usuario { Login = login, Nombre = nombre };
        nuevo.PasswordHash = auth.HashPassword(nuevo, password);
        await usuarios.CreateAsync(nuevo);
        Console.WriteLine($"Usuario '{login}' creado.");
    }
    else
    {
        await usuarios.CambiarPasswordAsync(existente.Id, auth.HashPassword(existente, password));
        Console.WriteLine($"Contraseña de '{existente.Login}' actualizada.");
    }

    return;
}

static string LeerSecreto(string pregunta)
{
    Console.Write(pregunta);
    if (Console.IsInputRedirected)          // sin terminal interactiva (docker exec sin -t, tuberías)
        return Console.ReadLine() ?? string.Empty;

    var sb = new System.Text.StringBuilder();
    ConsoleKeyInfo tecla;
    while ((tecla = Console.ReadKey(intercept: true)).Key != ConsoleKey.Enter)
    {
        if (tecla.Key == ConsoleKey.Backspace) { if (sb.Length > 0) sb.Length--; }
        else if (!char.IsControl(tecla.KeyChar)) sb.Append(tecla.KeyChar);
    }
    Console.WriteLine();
    return sb.ToString();
}

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


using Npgsql;
using System;
using System.IO;
using System.Linq;

namespace ComprasParaguai.Infrastructure;

public static class DbMigrator
{
    public static void Migrate(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        string targetDatabase = builder.Database ?? "comprasparaguai";
        
        // 1. Asegurar que la base de datos existe
        EnsureDatabaseExists(builder, targetDatabase);
        
        // 2. Ejecutar las migraciones en orden
        ExecuteMigrations(connectionString);
    }

    private static void EnsureDatabaseExists(NpgsqlConnectionStringBuilder targetBuilder, string dbName)
    {
        var systemBuilder = new NpgsqlConnectionStringBuilder(targetBuilder.ConnectionString)
        {
            Database = "postgres"
        };

        try
        {
            using var connection = new NpgsqlConnection(systemBuilder.ConnectionString);
            connection.Open();

            using var checkCmd = new NpgsqlCommand($"SELECT 1 FROM pg_database WHERE datname = '{dbName}'", connection);
            var exists = checkCmd.ExecuteScalar() != null;

            if (!exists)
            {
                Console.WriteLine($"Base de datos '{dbName}' no existe. Creándola...");
                using var createCmd = new NpgsqlCommand($"CREATE DATABASE \"{dbName}\"", connection);
                createCmd.ExecuteNonQuery();
                Console.WriteLine($"Base de datos '{dbName}' creada correctamente.");
            }
        }
        catch (PostgresException ex) when (ex.SqlState is "42P04" or "23505")
        {
            // Otro contenedor (API o worker) creó la base de datos al mismo tiempo: no es un error.
            Console.WriteLine($"La base de datos '{dbName}' ya la creó otro servicio.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al verificar/crear la base de datos '{dbName}': {ex.Message}");
            throw;
        }
    }

    private static void ExecuteMigrations(string connectionString)
    {
        string? migrationsPath = FindMigrationsDirectory();
        if (migrationsPath == null)
        {
            Console.WriteLine("ADVERTENCIA: No se encontró la carpeta 'db/migrations'. Saltando migraciones automáticas.");
            return;
        }

        Console.WriteLine($"Carpeta de migraciones encontrada en: {migrationsPath}");
        var sqlFiles = Directory.GetFiles(migrationsPath, "*.sql")
            .OrderBy(f => Path.GetFileName(f))
            .ToList();

        try
        {
            // Conexión propia sin pool: al cerrarla se libera el candado de inmediato (con pool se liberaría recién al reusarla).
            var sinPool = new NpgsqlConnectionStringBuilder(connectionString) { Pooling = false };
            using var connection = new NpgsqlConnection(sinPool.ConnectionString);
            connection.Open();

            // Candado de sesión: si la API y varios workers arrancan a la vez (Docker), migra uno solo y los demás esperan.
            // Se libera al terminar (y, de todos modos, al cerrar la conexión); las migraciones ya aplicadas se leen DESPUÉS de tomarlo.
            using (var lockCmd = new NpgsqlCommand("SELECT pg_advisory_lock(727001301)", connection))
            {
                lockCmd.CommandTimeout = 300;
                lockCmd.ExecuteNonQuery();
            }

            // Crear tabla de control de migraciones si no existe
            CreateMigrationsTable(connection);

            // Obtener migraciones ya aplicadas
            var appliedMigrations = GetAppliedMigrations(connection);

            foreach (var file in sqlFiles)
            {
                string fileName = Path.GetFileName(file);

                // Saltar si ya fue aplicada
                if (appliedMigrations.Contains(fileName))
                {
                    Console.WriteLine($"Migración {fileName} ya fue aplicada. Saltando...");
                    continue;
                }

                Console.WriteLine($"Aplicando migración: {fileName}...");
                string script = File.ReadAllText(file);

                using var cmd = new NpgsqlCommand(script, connection);
                cmd.ExecuteNonQuery();

                // Registrar migración como aplicada
                RegisterMigration(connection, fileName);
                Console.WriteLine($"Migración {fileName} aplicada con éxito.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error ejecutando las migraciones: {ex.Message}");
            throw;
        }
    }

    private static void CreateMigrationsTable(NpgsqlConnection connection)
    {
        const string createTableSql = @"
            CREATE TABLE IF NOT EXISTS schema_migrations (
                id SERIAL PRIMARY KEY,
                filename VARCHAR(255) NOT NULL UNIQUE,
                applied_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            );
        ";
        try
        {
            using var cmd = new NpgsqlCommand(createTableSql, connection);
            cmd.CommandTimeout = 30;
            cmd.ExecuteNonQuery();
            Console.WriteLine("✓ Tabla schema_migrations verificada/creada");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Advertencia al crear tabla de migraciones: {ex.Message}");
            // Continuar de todas formas, la tabla podría existir
        }
    }

    private static HashSet<string> GetAppliedMigrations(NpgsqlConnection connection)
    {
        var applied = new HashSet<string>();
        const string querySql = "SELECT filename FROM schema_migrations ORDER BY applied_at;";
        try
        {
            using var cmd = new NpgsqlCommand(querySql, connection);
            cmd.CommandTimeout = 30;
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                applied.Add(reader.GetString(0));
            }
            Console.WriteLine($"✓ Migraciones registradas: {applied.Count}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Advertencia al leer migraciones aplicadas: {ex.Message}");
            // Continuar sin lista de migraciones previas (ejecutará todas)
        }
        return applied;
    }

    private static void RegisterMigration(NpgsqlConnection connection, string filename)
    {
        const string insertSql = "INSERT INTO schema_migrations (filename) VALUES (@filename) ON CONFLICT (filename) DO NOTHING;";
        try
        {
            using var cmd = new NpgsqlCommand(insertSql, connection);
            cmd.CommandTimeout = 30;
            cmd.Parameters.AddWithValue("@filename", filename);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Advertencia al registrar migración {filename}: {ex.Message}");
            // No es crítico si falla el registro
        }
    }

    private static string? FindMigrationsDirectory()
    {
        // Buscar subiendo desde el directorio base de ejecución hasta encontrar 'db/migrations'
        string? current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            string candidate = Path.Combine(current, "db", "migrations");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
            string? parent = Directory.GetParent(current)?.FullName;
            if (parent == current) break;
            current = parent;
        }

        // Buscar también desde el directorio actual (Cwd)
        current = Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(current))
        {
            string candidate = Path.Combine(current, "db", "migrations");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
            string? parent = Directory.GetParent(current)?.FullName;
            if (parent == current) break;
            current = parent;
        }

        return null;
    }
}

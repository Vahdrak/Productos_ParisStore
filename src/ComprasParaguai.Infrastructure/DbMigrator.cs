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
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            foreach (var file in sqlFiles)
            {
                string fileName = Path.GetFileName(file);
                Console.WriteLine($"Aplicando migración: {fileName}...");
                string script = File.ReadAllText(file);

                using var cmd = new NpgsqlCommand(script, connection);
                cmd.ExecuteNonQuery();
                Console.WriteLine($"Migración {fileName} aplicada con éxito.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error ejecutando las migraciones: {ex.Message}");
            throw;
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

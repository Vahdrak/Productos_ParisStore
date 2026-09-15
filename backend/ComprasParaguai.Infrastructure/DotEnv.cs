namespace ComprasParaguai.Infrastructure;

/// <summary>
/// Carga un archivo .env (KEY=VALUE por línea) como variables de entorno del proceso,
/// para que IConfiguration las tome. Usar "__" como separador de sección: Compras__Token.
/// No pisa variables que ya existan en el entorno.
/// </summary>
public static class DotEnv
{
    public static void Load(string path)
    {
        if (!File.Exists(path))
            return;

        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            int eq = line.IndexOf('=');
            if (eq <= 0)
                continue;

            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();

            if (value.Length >= 2 && (value[0] == '"' || value[0] == '\'') && value[^1] == value[0])
                value = value[1..^1];

            if (Environment.GetEnvironmentVariable(key) is null)
                Environment.SetEnvironmentVariable(key, value);
        }
    }
}

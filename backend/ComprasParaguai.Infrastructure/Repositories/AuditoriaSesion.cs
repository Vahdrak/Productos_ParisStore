using ComprasParaguai.Core.Models;
using Npgsql;

namespace ComprasParaguai.Infrastructure.Repositories;

/// <summary>
/// Fija en la transacción actual quién origina los cambios (app.origen / app.usuario),
/// para que el trigger de auditoría los guarde en productos_historial.
/// set_config(..., true) vale solo hasta el final de la transacción.
/// </summary>
internal static class AuditoriaSesion
{
    public static async Task ConfigurarAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, CambioContexto contexto, CancellationToken cancellationToken)
    {
        await using var cmd = new NpgsqlCommand(
            "SELECT set_config('app.origen', @origen, true), set_config('app.usuario', @usuario, true)",
            connection, transaction);
        cmd.Parameters.AddWithValue("origen", contexto.OrigenDb);
        cmd.Parameters.AddWithValue("usuario", (object?)contexto.Usuario ?? string.Empty);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}

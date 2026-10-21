namespace ComprasParaguai.Core.Models;

/// <summary>Quién origina un cambio sobre productos; queda en productos_historial.</summary>
public enum OrigenCambio
{
    /// <summary>Sincronización periódica Oracle → PostgreSQL.</summary>
    Sync,
    /// <summary>Reconciliación diaria (bajas detectadas).</summary>
    Reconciliacion,
    /// <summary>Edición desde la API (PUT).</summary>
    Api
}

/// <summary>
/// Contexto de auditoría de una escritura: de dónde viene y quién la hizo.
/// El repositorio lo fija en la transacción (app.origen / app.usuario) y lo lee el trigger de auditoría.
/// </summary>
public record CambioContexto(OrigenCambio Origen, string? Usuario = null)
{
    /// <summary>Valor guardado en productos_historial.origen.</summary>
    public string OrigenDb => Origen switch
    {
        OrigenCambio.Sync => "sync",
        OrigenCambio.Reconciliacion => "reconciliacion",
        OrigenCambio.Api => "api",
        _ => "manual"
    };

    public static CambioContexto Sync => new(OrigenCambio.Sync);
    public static CambioContexto Reconciliacion => new(OrigenCambio.Reconciliacion);
}

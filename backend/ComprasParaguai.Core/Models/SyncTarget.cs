namespace ComprasParaguai.Core.Models;

/// <summary>
/// Destino de envío. Cada uno tiene su propia columna de estado en <c>productos</c>:
/// ComprasParaguai → sync_status / last_sent_at / last_error;
/// ComparaParaguai → sync_compara_status / last_sent_compara_at / last_error_compara.
/// </summary>
public enum SyncTarget
{
    ComprasParaguai,
    ComparaParaguai
}

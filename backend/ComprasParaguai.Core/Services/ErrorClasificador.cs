namespace ComprasParaguai.Core.Services;

/// <summary>Tipo de error de envío, con una explicación y qué hacer.</summary>
/// <param name="Codigo">Identificador estable (se usa en filtros).</param>
/// <param name="Titulo">Nombre corto para mostrar.</param>
/// <param name="QueHacer">Cómo proceder.</param>
/// <param name="Reintentable">True si reintentar (volver a Pendiente) tiene sentido sin cambiar el dato.</param>
public record ErrorTipo(string Codigo, string Titulo, string QueHacer, bool Reintentable);

/// <summary>
/// Agrupa los mensajes de error que guardan los envíos (last_error / last_error_compara) en tipos conocidos,
/// para poder verlos agrupados y saber cómo proceder.
/// </summary>
public static class ErrorClasificador
{
    public static readonly ErrorTipo Duplicado = new("duplicado", "Duplicado en el destino (slug repetido)",
        "El destino ya tiene otro producto con el mismo nombre/código y lo rechaza. Cambia el nombre o el código en Cangrejo, " +
        "o pide al comparador que lo corrija. Reintentar sin cambiar nada vuelve a fallar; si el dato cambia, el próximo sync lo reenvía solo.",
        false);

    public static readonly ErrorTipo Imagen = new("imagen", "Imagen con formato no soportado",
        "Solo se aceptan png, jpg, jpeg y webp. El sync ya usa la imagen por defecto en esos casos: tras el próximo sync se reenvían solos; si no, usa Reintentar.",
        true);

    public static readonly ErrorTipo Precio = new("precio", "Precio fuera de rango",
        "El precio debe estar entre 0,05 y 99.999 (0,05 exacto también se rechaza). Corrige el precio en Cangrejo.",
        false);

    public static readonly ErrorTipo Codigo = new("codigo", "Código (SKU) de más de 20 caracteres",
        "Compras acepta códigos de hasta 20 caracteres. Corrige el SKU en Cangrejo.",
        false);

    public static readonly ErrorTipo Credenciales = new("credenciales", "Credenciales o token rechazados",
        "Revisa el token del worker (su .env). Luego usa Reintentar.",
        true);

    public static readonly ErrorTipo Temporal = new("temporal", "Error temporal del destino o de la red",
        "Suele ser transitorio (servidor caído, tiempo de espera, límite de pedidos). Usa Reintentar; si se repite, revisa el mensaje.",
        true);

    public static readonly ErrorTipo Otro = new("otro", "Otro error",
        "Revisa el mensaje. Si ya está corregido, usa Reintentar.",
        true);

    public static readonly IReadOnlyList<ErrorTipo> Todos = new[] { Duplicado, Imagen, Precio, Codigo, Credenciales, Temporal, Otro };

    /// <summary>
    /// Clasifica un mensaje. Un mensaje que empieza con "Retiro:" es de la etapa de retiro del destino
    /// (<paramref name="esRetiro"/>) y se clasifica por el texto que sigue.
    /// </summary>
    public static ErrorTipo Clasificar(string? mensaje, out bool esRetiro)
    {
        string m = (mensaje ?? string.Empty).Trim();
        esRetiro = m.StartsWith("Retiro:", StringComparison.OrdinalIgnoreCase);
        if (esRetiro)
            m = m["Retiro:".Length..].Trim();

        // Los errores de producto (validación) primero: el HTTP 500 de un duplicado no es "temporal".
        if (Contiene(m, "Duplicate entry"))
            return Duplicado;
        if (Contiene(m, "url_image"))
            return Imagen;
        if (Contiene(m, "price:") || Contiene(m, "price_iva:"))
            return Precio;
        if (Contiene(m, "code:") && Contiene(m, "characters"))
            return Codigo;
        if (Contiene(m, "Unauthorized") || Contiene(m, "Forbidden") || Contiene(m, "Token is invalid"))
            return Credenciales;
        if (Contiene(m, "InternalServerError") || Contiene(m, "BadGateway") || Contiene(m, "ServiceUnavailable")
            || Contiene(m, "GatewayTimeout") || Contiene(m, "TooManyRequests") || Contiene(m, "timed out")
            || Contiene(m, "timeout") || Contiene(m, "connection") || Contiene(m, "refused") || Contiene(m, "socket")
            || Contiene(m, "name resolution") || Contiene(m, "canceled"))
            return Temporal;

        return Otro;
    }

    public static ErrorTipo? PorCodigo(string? codigo) =>
        Todos.FirstOrDefault(t => string.Equals(t.Codigo, codigo, StringComparison.OrdinalIgnoreCase));

    private static bool Contiene(string texto, string fragmento) =>
        texto.Contains(fragmento, StringComparison.OrdinalIgnoreCase);
}

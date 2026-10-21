namespace ComprasParaguai.Core.Models;

/// <summary>Producto pendiente de envío, indicando si hoy está retenido (y por qué regla).</summary>
public class ProductoPendiente : ProductoApi
{
    /// <summary>True si una pausa o excepción lo retiene: no se enviará mientras dure.</summary>
    public bool Retenido { get; set; }

    /// <summary>Qué lo retiene: "LACOSTE en INDUMENTARIA", "Excepción: retener SKU", etc.</summary>
    public string? RetenidoPor { get; set; }
}

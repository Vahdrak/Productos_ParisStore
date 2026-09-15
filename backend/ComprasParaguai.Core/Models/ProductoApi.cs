namespace ComprasParaguai.Core.Models;

/// <summary>
/// Producto enriquecido, listo para enviar a la API de Compras Paraguai.
/// Combina campos de Cangrejo (refrescados cada sync) + campos propios (preservados).
/// </summary>
public class ProductoApi
{
    /// <summary>SKU (llave del upsert en la API).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Nombre en español (de Cangrejo).</summary>
    public string NameEs { get; set; } = string.Empty;

    /// <summary>Nombre en portugués (propio, enriquecido).</summary>
    public string NamePt { get; set; } = string.Empty;

    /// <summary>Precio normal en PYG. Rango válido: 0.05 – 99999.</summary>
    public decimal Price { get; set; }

    /// <summary>Precio con IVA (10% Paraguay). Opcional.</summary>
    public decimal? PriceIva { get; set; }

    /// <summary>Stock disponible. Negativos → 0. 0 = no disponible.</summary>
    public int Stock { get; set; }

    /// <summary>Marca (de Cangrejo cuando NOMBRE_ATRIBUTO_1 = "Marca").</summary>
    public string? Brand { get; set; }

    /// <summary>Descripción en español (propio).</summary>
    public string DescriptionEs { get; set; } = string.Empty;

    /// <summary>Descripción en portugués (propio).</summary>
    public string DescriptionPt { get; set; } = string.Empty;

    /// <summary>Nombre de la imagen (de Cangrejo). Ej: "producto123.jpg".</summary>
    public string UrlImage { get; set; } = string.Empty;

    /// <summary>URL completa de la imagen. Ej: https://www.toku.com.py/media/producto123.jpg (vacío si no existe).</summary>
    public string Link { get; set; } = string.Empty;

    /// <summary>Link de compra directo (opcional).</summary>
    public string? LinkPurchase { get; set; }

    /// <summary>Forzar refresco de imagen en el comparador (típicamente false).</summary>
    public bool ForceImageUpdate { get; set; } = false;

    /// <summary>Hash SHA-256 del payload final. Se recalcula en cada sync para detectar cambios.</summary>
    public string? SourceHash { get; set; }

    /// <summary>Estado de sincronización: 0=Pendiente, 1=Enviado, 2=Error.</summary>
    public int SyncStatus { get; set; }

    /// <summary>Categoría del producto.</summary>
    public string? Categoria { get; set; }
}


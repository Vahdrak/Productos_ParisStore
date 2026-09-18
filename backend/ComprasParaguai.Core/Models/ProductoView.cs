namespace ComprasParaguai.Core.Models;

/// <summary>
/// Fila tal como viene de SENSEI.PARAGUAY_COMPRAS_VIEW (Cangrejo).
/// Solo lectura, sin modificaciones.
/// </summary>
public class ProductoView
{
    /// <summary>SKU / código de barras / EAN — API: code (llave del upsert).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Nombre del producto en español (de Cangrejo).</summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Precio normal en moneda local (PYG).</summary>
    public decimal? PrecioNormal { get; set; }

    /// <summary>Régimen de Turismo de Oracle ("Si" o "No").</summary>
    public string? RegimenTurismo { get; set; }

    /// <summary>Stock en inventario (puede ser negativo en Cangrejo).</summary>
    public int? Inventario { get; set; }

    /// <summary>Valor del atributo 1 (típicamente la marca cuando NOMBRE_ATRIBUTO_1 = "Marca").</summary>
    public string? ValorAtributo1 { get; set; }

    /// <summary>Nombre del atributo 1.</summary>
    public string? NombreAtributo1 { get; set; }

    /// <summary>Ruta de categoría en el árbol (ej. "TOKU>COMESTIBLES>CHOCOLATES").</summary>
    public string? Categoria { get; set; }

    /// <summary>Nombre o URL del archivo de imagen (ej. "prod_976990101.webp" o "sinimagen.jpg").</summary>
    public string? Imagenes { get; set; }

    /// <summary>JSON raw de la fila original, para auditoría.</summary>
    public string? RawJson { get; set; }
}

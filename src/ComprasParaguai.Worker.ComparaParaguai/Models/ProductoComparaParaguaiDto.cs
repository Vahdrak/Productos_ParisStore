using System.Text.Json.Serialization;

namespace ComprasParaguai.Worker.ComparaParaguai.Models;

/// <summary>
/// DTO para enviar a Compara Paraguai API.
/// Solo incluye los campos que la API espera, con nombres en snake_case.
/// </summary>
public class ProductoComparaParaguaiDto
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("name_es")]
    public string NameEs { get; set; } = string.Empty;

    [JsonPropertyName("name_pt")]
    public string NamePt { get; set; } = string.Empty;

    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    [JsonPropertyName("price_iva")]
    public decimal? PriceIva { get; set; }

    [JsonPropertyName("stock")]
    public int Stock { get; set; }

    [JsonPropertyName("brand")]
    public string? Brand { get; set; }

    [JsonPropertyName("description_es")]
    public string DescriptionEs { get; set; } = string.Empty;

    [JsonPropertyName("description_pt")]
    public string DescriptionPt { get; set; } = string.Empty;

    [JsonPropertyName("url_image")]
    public string UrlImage { get; set; } = string.Empty;

    [JsonPropertyName("link")]
    public string Link { get; set; } = string.Empty;

    [JsonPropertyName("link_purchase")]
    public string? LinkPurchase { get; set; }
}

namespace ComprasParaguai.Core.Models;

/// <summary>Producto tal como lo devuelve la API de un destino (GET por código).</summary>
public record RemoteProduct(
    string Code,
    string? NamePt,
    decimal? Price,
    decimal? PriceIva,
    decimal? Stock,
    bool? Available,
    string? Link,
    DateTime? UpdatedAt);

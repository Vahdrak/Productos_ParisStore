using ComprasParaguai.Core.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ComprasParaguai.Core.Services;

/// <summary>
/// Mapea ProductoView (de Cangrejo) → ProductoApi (formato API).
/// Aplica limpieza (stock negativo→0, validación de precio, URL de imagen por defecto).
/// Calcula source_hash (SHA-256) del payload final.
/// </summary>
public class ProductoMapperService
{
    private readonly string _imageBaseUrl;
    private readonly string _imagenPorDefecto;
    private readonly string _linkPattern;

    /// <summary>
    /// Crea el servicio con parámetros de enriquecimiento.
    /// </summary>
    /// <param name="imageBaseUrl">URL base de imágenes (ej. "https://images.toku.com/").</param>
    /// <param name="imagenPorDefecto">URL completa de imagen por defecto si no existe.</param>
    /// <param name="linkPattern">Patrón de URL del producto (ej. "https://toku.com/producto/{code}").</param>
    public ProductoMapperService(string imageBaseUrl, string imagenPorDefecto, string linkPattern)
    {
        _imageBaseUrl = imageBaseUrl ?? throw new ArgumentNullException(nameof(imageBaseUrl));
        _imagenPorDefecto = imagenPorDefecto ?? throw new ArgumentNullException(nameof(imagenPorDefecto));
        _linkPattern = linkPattern ?? throw new ArgumentNullException(nameof(linkPattern));
    }

    /// <summary>
    /// Mapea y enriquece un ProductoView → ProductoApi, aplicando todas las reglas de limpieza.
    /// Retorna null si el producto no cumple validaciones críticas (ej. price fuera de rango).
    /// </summary>
    public ProductoApi? MapAndValidate(ProductoView view)
    {
        if (view == null)
            return null;

        if (string.IsNullOrWhiteSpace(view.Code))
            return null;

        // Stock: negativos → 0.
        int stock = Math.Max(0, view.Inventario ?? 0);

        // Precio: validar rango [0.05, 99999]. Si inválido → null (saltar producto).
        decimal? price = view.PrecioNormal;
        if (price.HasValue && (price < 0.05m || price > 99999m))
            return null; // Producto inválido, registrar y saltar.

        if (!price.HasValue)
            return null; // Sin precio → no se puede enviar.

        // Régimen de Turismo: convertir "Si"/"No" de Oracle → boolean
        // Usar ToUpper() para manejar variaciones de capitalización ("Si", "SI", "si", etc.)
        bool regimenTurismo = (view.RegimenTurismo?.Trim()?.ToUpper() ?? "") == "SI";

        // Precio exenta (sin IVA): solo si es régimen turismo
        decimal? precioExenta = null;
        if (regimenTurismo && price.HasValue)
        {
            precioExenta = price.Value - (price.Value / 11);
        }

        // Nombre en portugués: por ahora, duplicar el español (MVP). Luego: traducir.
        string namePt = view.Nombre ?? "Sin nombre";

        // Descripción: nombre + categoría normalizada, sin la empresa ("... - COCINA > VAJILLAS > PLATILLOS").
        string descriptionEs = $"{view.Nombre}";
        string categoriaTexto = CategoriaTexto.ParaMostrar(view.Categoria);
        if (categoriaTexto.Length > 0)
            descriptionEs += $" - {categoriaTexto}";

        string descriptionPt = descriptionEs; // MVP: mismo en portugués.

        // Marca: extraer de ValorAtributo1 si NombreAtributo1 = "Marca".
        string? brand = null;
        if (!string.IsNullOrWhiteSpace(view.NombreAtributo1) &&
            view.NombreAtributo1.Equals("Marca", StringComparison.OrdinalIgnoreCase))
        {
            brand = view.ValorAtributo1;
        }

        // URL de imagen: debe ser absoluta (Compras Paraguai la valida). base + archivo, o la imagen por defecto.
        string urlImage = _imagenPorDefecto;
        if (!string.IsNullOrWhiteSpace(view.Imagenes) &&
            !view.Imagenes.Equals("sinimagen.jpg", StringComparison.OrdinalIgnoreCase))
        {
            urlImage = _imageBaseUrl.TrimEnd('/') + "/" + view.Imagenes.Trim().TrimStart('/');
        }

        // Link a la página del producto en la tienda (patrón configurable con {code}).
        string link = _linkPattern.Replace("{code}", Uri.EscapeDataString(view.Code));

        // Armar ProductoApi.
        var api = new ProductoApi
        {
            Code = view.Code,
            NameEs = view.Nombre ?? string.Empty,
            NamePt = namePt,
            Price = price.Value,
            PriceIva = price.Value,
            RegimenTurismo = regimenTurismo,
            PrecioExenta = precioExenta,
            Stock = stock,
            Brand = brand,
            DescriptionEs = descriptionEs,
            DescriptionPt = descriptionPt,
            UrlImage = urlImage,
            Link = link,
            LinkPurchase = null,
            ForceImageUpdate = false,
            Categoria = view.Categoria
        };

        // Calcular hash del payload final.
        api.SourceHash = CalculateSourceHash(api);

        return api;
    }

    /// <summary>
    /// Calcula SHA-256 del payload final (JSON serializado de ProductoApi sin el hash).
    /// Se usa para detectar cambios en cada sync.
    /// </summary>
    public string CalculateSourceHash(ProductoApi api)
    {
        // Crear un payload sin el SourceHash para calcular el hash.
        var payload = new
        {
            api.Code,
            api.NameEs,
            api.NamePt,
            api.Price,
            api.PriceIva,
            api.RegimenTurismo,
            api.PrecioExenta,
            api.Stock,
            api.Brand,
            api.DescriptionEs,
            api.DescriptionPt,
            api.UrlImage,
            api.Link,
            api.LinkPurchase,
            api.ForceImageUpdate
        };

        string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = false });
        byte[] bytes = Encoding.UTF8.GetBytes(json);

        using (var sha256 = SHA256.Create())
        {
            byte[] hashBytes = sha256.ComputeHash(bytes);
            return Convert.ToHexString(hashBytes).ToLower();
        }
    }
}

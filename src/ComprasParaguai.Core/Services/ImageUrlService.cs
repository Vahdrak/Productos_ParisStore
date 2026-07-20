namespace ComprasParaguai.Core.Services;

/// <summary>
/// Servicio para construir URLs completas de imágenes.
/// Si la imagen existe (no es vacía ni "sinimagen.jpg"), concatena con URL base.
/// Si no existe, retorna vacío.
/// </summary>
public class ImageUrlService
{
    private readonly string _baseImageUrl;

    public ImageUrlService(string baseImageUrl = "https://www.toku.com.py/media/")
    {
        _baseImageUrl = baseImageUrl;
    }

    /// <summary>
    /// Construye la URL completa de la imagen.
    /// Si imageName es nulo, vacío o "sinimagen.jpg", retorna string.Empty.
    /// Si no, retorna baseUrl + imageName.
    /// </summary>
    public string BuildImageUrl(string? imageName)
    {
        if (string.IsNullOrWhiteSpace(imageName) || imageName == "sinimagen.jpg")
            return string.Empty;

        return $"{_baseImageUrl}{imageName}";
    }
}

using System.Text.RegularExpressions;

namespace ComprasParaguai.Core.Services;

/// <summary>
/// Normalización del texto de categoría que entrega Cangrejo (ruta separada por '>').
/// Es el espejo en C# de fn_normalizar_ruta_categoria (migración 009): la BD usa la función SQL
/// para armar el árbol, y esta clase se usa para el texto que se envía en la descripción.
/// </summary>
public static class CategoriaTexto
{
    private const char Separador = '>';
    private static readonly Regex Espacios = new(@"\s+", RegexOptions.Compiled);

    /// <summary>
    /// Cangrejo entrega algunas letras como katakana de ancho medio (U+FF61..U+FF9F):
    /// "BAﾑO" en vez de "BAÑO". El carácter correcto es cp - 0xFF61 + 0xA1 (Latin-1).
    /// </summary>
    public static string CorregirCodificacion(string texto)
    {
        if (string.IsNullOrEmpty(texto))
            return texto;

        return string.Create(texto.Length, texto, (span, src) =>
        {
            for (int i = 0; i < src.Length; i++)
            {
                char c = src[i];
                span[i] = c >= '｡' && c <= 'ﾟ' ? (char)(c - 0xFF61 + 0xA1) : c;
            }
        });
    }

    /// <summary>
    /// "A>B>B>C " → ["A","B","C"]: corrige la codificación, recorta, descarta segmentos vacíos y
    /// colapsa segmentos consecutivos iguales (Cangrejo repite el nivel cuando no lo usa).
    /// </summary>
    public static List<string> Segmentos(string? ruta)
    {
        var resultado = new List<string>();
        if (string.IsNullOrWhiteSpace(ruta))
            return resultado;

        foreach (var crudo in CorregirCodificacion(ruta).Split(Separador))
        {
            var seg = Espacios.Replace(crudo.Trim(), " ");
            if (seg.Length == 0)
                continue;
            if (resultado.Count > 0 && string.Equals(resultado[^1], seg, StringComparison.OrdinalIgnoreCase))
                continue;
            resultado.Add(seg);
        }

        return resultado;
    }

    /// <summary>Ruta normalizada, unida con '>'. Null si no queda ningún segmento.</summary>
    public static string? Normalizar(string? ruta)
    {
        var segs = Segmentos(ruta);
        return segs.Count == 0 ? null : string.Join(Separador, segs);
    }

    /// <summary>
    /// Texto para mostrar en la descripción: sin el nivel 1 (la empresa) y unido con " > ".
    /// "TOKU IMPORTADOS>COCINA>VAJILLAS>VAJILLAS>PLATILLOS" → "COCINA > VAJILLAS > PLATILLOS".
    /// </summary>
    public static string ParaMostrar(string? ruta)
    {
        var segs = Segmentos(ruta);
        if (segs.Count > 1)
            segs.RemoveAt(0);
        else
            return string.Empty;

        return string.Join(" > ", segs);
    }
}

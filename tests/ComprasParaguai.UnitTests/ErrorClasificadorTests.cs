using ComprasParaguai.Core.Services;
using Xunit;

namespace ComprasParaguai.UnitTests;

public class ErrorClasificadorTests
{
    // Mensajes reales de producción.
    [Theory]
    [InlineData("HTTP InternalServerError: {\"success\":false,\"message\":\"Unexpected error during import.\",\"errors\":\"(1062, \\\"Duplicate entry 'ecopower-ep-tv032-smart-tv-EPTV032202310192000-889' for key 'pesquisa_produto.slug'\\\")\"}", "duplicado")]
    [InlineData("url_image: Value error, The URL image format is not supported. Supported formats: .png, .jpg, .jpeg, .webp", "imagen")]
    [InlineData("price: Value error, The price must be between 0.05 and 99999.; price_iva: Value error, The price_iva must be between 0.05 and 99999.", "precio")]
    [InlineData("code: Value error, The code field must have at most 20 characters.", "codigo")]
    [InlineData("HTTP Unauthorized: Token is invalid or expired", "credenciales")]
    [InlineData("HTTP BadGateway: <html>502</html>", "temporal")]
    [InlineData("HTTP InternalServerError: Something broke", "temporal")]
    [InlineData("The request was canceled due to the configured HttpClient.Timeout of 100 seconds elapsing.", "temporal")]
    [InlineData("algo que nunca vimos", "otro")]
    [InlineData(null, "otro")]
    public void Clasifica(string? mensaje, string esperado)
    {
        Assert.Equal(esperado, ErrorClasificador.Clasificar(mensaje).Codigo);
    }

    [Fact]
    public void Duplicado_NoSeConfundeConErrorTemporal_AunqueSea500()
    {
        var tipo = ErrorClasificador.Clasificar("HTTP InternalServerError: ... Duplicate entry 'x' ...");

        Assert.Equal("duplicado", tipo.Codigo);
        Assert.False(tipo.Reintentable);
    }

    [Fact]
    public void PorCodigo_EncuentraElTipo_YDevuelveNullSiNoExiste()
    {
        Assert.Same(ErrorClasificador.Imagen, ErrorClasificador.PorCodigo("IMAGEN"));
        Assert.Null(ErrorClasificador.PorCodigo("inventado"));
    }
}

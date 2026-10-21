using ComprasParaguai.Core.Services;
using Xunit;

namespace ComprasParaguai.UnitTests;

public class CategoriaTextoTests
{
    [Theory]
    [InlineData("TOKU IMPORTADOS>JUGUETERIA>JUGUETES NIﾑAS", "TOKU IMPORTADOS>JUGUETERIA>JUGUETES NIÑAS")]
    [InlineData("TOKU IMPORTADOS>MODA>CAFﾉ", "TOKU IMPORTADOS>MODA>CAFÉ")]
    [InlineData("TOKU IMPORTADOS>COCINA>CUBERTERIA", "TOKU IMPORTADOS>COCINA>CUBERTERIA")]
    public void CorregirCodificacion_ReemplazaKatakanaPorLatin1(string entrada, string esperado)
    {
        Assert.Equal(esperado, CategoriaTexto.CorregirCodificacion(entrada));
    }

    [Theory]
    [InlineData("TOKU IMPORTADOS>COCINA>VAJILLAS>VAJILLAS>PLATILLOS", "TOKU IMPORTADOS>COCINA>VAJILLAS>PLATILLOS")]
    [InlineData("TOKU IMPORTADOS>COCINA>OUTLET>OUTLET>OUTLET", "TOKU IMPORTADOS>COCINA>OUTLET")]
    [InlineData("TOKU IMPORTADOS>cocina>COCINA>ollas", "TOKU IMPORTADOS>cocina>ollas")]
    [InlineData(" TOKU IMPORTADOS > COCINA  >  OLLAS ", "TOKU IMPORTADOS>COCINA>OLLAS")]
    [InlineData("TOKU IMPORTADOS>>COCINA>", "TOKU IMPORTADOS>COCINA")]
    public void Normalizar_RecortaColapsaRepetidosYDescartaVacios(string entrada, string esperado)
    {
        Assert.Equal(esperado, CategoriaTexto.Normalizar(entrada));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(">>")]
    public void Normalizar_SinSegmentos_RetornaNull(string? entrada)
    {
        Assert.Null(CategoriaTexto.Normalizar(entrada));
    }

    [Fact]
    public void Normalizar_NoColapsaNombresIgualesNoConsecutivos()
    {
        Assert.Equal("A>B>A", CategoriaTexto.Normalizar("A>B>A"));
    }

    [Theory]
    [InlineData("TOKU IMPORTADOS>COCINA>VAJILLAS>VAJILLAS>PLATILLOS PLASTICO", "COCINA > VAJILLAS > PLATILLOS PLASTICO")]
    [InlineData("TOKU IMPORTADOS>CAMPING", "CAMPING")]
    [InlineData("TOKU IMPORTADOS", "")]
    [InlineData(null, "")]
    public void ParaMostrar_QuitaLaEmpresaYUneConFlecha(string? entrada, string esperado)
    {
        Assert.Equal(esperado, CategoriaTexto.ParaMostrar(entrada));
    }
}

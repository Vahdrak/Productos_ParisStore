using ComprasParaguai.Core.Models;
using ComprasParaguai.Core.Services;
using Xunit;

namespace ComprasParaguai.UnitTests;

public class ProductoMapperServiceTests
{
    private readonly ProductoMapperService _mapper = new(
        imageBaseUrl: "https://images.example.com/",
        imagenPorDefecto: "https://images.example.com/default.png",
        linkPattern: "https://shop.example.com/producto/{code}"
    );

    [Fact]
    public void MapAndValidate_ValidProduct_ReturnsMapped()
    {
        var view = new ProductoView
        {
            Code = "8806097656784",
            Nombre = "Samsung SM-A075F 4GB/64GB Black",
            PrecioNormal = 97,
            Inventario = 10,
            ValorAtributo1 = "Samsung",
            NombreAtributo1 = "Marca",
            Categoria = "ELECTRÓNICA>CELULARES",
            Imagenes = "prod_976990101.webp"
        };

        var result = _mapper.MapAndValidate(view);

        Assert.NotNull(result);
        Assert.Equal("8806097656784", result.Code);
        Assert.Equal("Samsung SM-A075F 4GB/64GB Black", result.NameEs);
        Assert.Equal(97, result.Price);
        Assert.Equal(10, result.Stock);
        Assert.Equal("Samsung", result.Brand);
        Assert.StartsWith("https://images.example.com/", result.UrlImage);
        Assert.Contains("8806097656784", result.Link);
    }

    [Fact]
    public void MapAndValidate_Description_UsesNormalizedCategoryWithoutCompany()
    {
        var view = new ProductoView
        {
            Code = "DESC-001",
            Nombre = "PLATO LLANO",
            PrecioNormal = 10,
            Inventario = 1,
            Categoria = "TOKU IMPORTADOS>COCINA>VAJILLAS>VAJILLAS>PLATILLOS PLASTICO"
        };

        var result = _mapper.MapAndValidate(view);

        Assert.NotNull(result);
        Assert.Equal("PLATO LLANO - COCINA > VAJILLAS > PLATILLOS PLASTICO", result.DescriptionEs);
        Assert.Equal(result.DescriptionEs, result.DescriptionPt);
        // La categoría cruda se conserva para que la BD arme el árbol.
        Assert.Equal("TOKU IMPORTADOS>COCINA>VAJILLAS>VAJILLAS>PLATILLOS PLASTICO", result.Categoria);
    }

    [Fact]
    public void MapAndValidate_Description_FixesBrokenEncoding()
    {
        var view = new ProductoView
        {
            Code = "DESC-002",
            Nombre = "MUﾑECA",
            PrecioNormal = 10,
            Inventario = 1,
            Categoria = "TOKU IMPORTADOS>JUGUETERIA>JUGUETES NIﾑAS"
        };

        var result = _mapper.MapAndValidate(view);

        Assert.NotNull(result);
        Assert.EndsWith(" - JUGUETERIA > JUGUETES NIÑAS", result.DescriptionEs);
    }

    [Fact]
    public void MapAndValidate_Description_WithoutCategory_IsJustTheName()
    {
        var view = new ProductoView { Code = "DESC-003", Nombre = "SIN CATEGORIA", PrecioNormal = 10, Inventario = 1 };

        var result = _mapper.MapAndValidate(view);

        Assert.NotNull(result);
        Assert.Equal("SIN CATEGORIA", result.DescriptionEs);
    }

    [Fact]
    public void MapAndValidate_NegativeStock_ReturnsZero()
    {
        var view = new ProductoView
        {
            Code = "TEST-001",
            Nombre = "Test Product",
            PrecioNormal = 50,
            Inventario = -5
        };

        var result = _mapper.MapAndValidate(view);

        Assert.NotNull(result);
        Assert.Equal(0, result.Stock);
    }

    [Fact]
    public void MapAndValidate_InvalidPrice_ReturnsNull()
    {
        var view = new ProductoView
        {
            Code = "TEST-002",
            Nombre = "Invalid Price",
            PrecioNormal = 150000, // Fuera de rango [0.05, 99999]
            Inventario = 10
        };

        var result = _mapper.MapAndValidate(view);

        Assert.Null(result);
    }

    [Fact]
    public void MapAndValidate_DefaultImage_UsesDefault()
    {
        var view = new ProductoView
        {
            Code = "TEST-003",
            Nombre = "No Image Product",
            PrecioNormal = 25,
            Inventario = 5,
            Imagenes = "sinimagen.jpg"
        };

        var result = _mapper.MapAndValidate(view);

        Assert.NotNull(result);
        Assert.Equal("https://images.example.com/default.png", result.UrlImage);
    }

    [Fact]
    public void CalculateSourceHash_DifferentProducts_DifferentHashes()
    {
        var api1 = new ProductoApi
        {
            Code = "TEST-1",
            NameEs = "Product 1",
            NamePt = "Produto 1",
            Price = 10,
            Stock = 5,
            UrlImage = "https://example.com/1.png",
            Link = "https://shop.com/1"
        };

        var api2 = new ProductoApi
        {
            Code = "TEST-2",
            NameEs = "Product 2",
            NamePt = "Produto 2",
            Price = 20,
            Stock = 10,
            UrlImage = "https://example.com/2.png",
            Link = "https://shop.com/2"
        };

        string hash1 = _mapper.CalculateSourceHash(api1);
        string hash2 = _mapper.CalculateSourceHash(api2);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void CalculateSourceHash_SameProduct_SameHash()
    {
        var api = new ProductoApi
        {
            Code = "TEST-1",
            NameEs = "Product",
            NamePt = "Produto",
            Price = 50,
            Stock = 10,
            UrlImage = "https://example.com/img.png",
            Link = "https://shop.com/product"
        };

        string hash1 = _mapper.CalculateSourceHash(api);
        string hash2 = _mapper.CalculateSourceHash(api);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void CalculateSourceHash_Format_IsHexadecimal64Chars()
    {
        var api = new ProductoApi
        {
            Code = "TEST",
            NameEs = "Name",
            NamePt = "Nome",
            Price = 10,
            Stock = 5,
            UrlImage = "https://example.com/img.png",
            Link = "https://shop.com"
        };

        string hash = _mapper.CalculateSourceHash(api);

        // SHA-256 en hex es 64 caracteres.
        Assert.Equal(64, hash.Length);
        Assert.True(hash.All(c => "0123456789abcdef".Contains(c)));
    }
}

using ComprasParaguai.Core.Interfaces;
using ComprasParaguai.Core.Models;
using ComprasParaguai.Core.Services;
using Moq;
using Xunit;

namespace ComprasParaguai.UnitTests;

public class SyncServiceTests
{
    [Fact]
    public async Task SyncAsync_NewProduct_IsMarkedAsNew()
    {
        // Arrange
        var mockReader = new Mock<IViewReader>();
        var mockRepository = new Mock<IProductoRepository>();

        var view = new ProductoView
        {
            Code = "NEW-001",
            Nombre = "Nuevo Producto",
            PrecioNormal = 50,
            Inventario = 10
        };

        mockReader
            .Setup(r => r.ReadAsync(It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(view));

        mockRepository
            .Setup(r => r.LoadProductHashMapAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string?>());

        var mapper = new ProductoMapperService(
            "https://img.test/",
            "https://img.test/default.png",
            "https://shop.test/{code}"
        );

        var service = new SyncService(mockReader.Object, mockRepository.Object, mapper);

        // Act
        var result = await service.SyncAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, result.NewProducts);
        Assert.Equal(0, result.UpdatedProducts);
        Assert.Equal(0, result.UnchangedProducts);

        mockRepository.Verify(
            r => r.UpsertProductsAsync(It.IsAny<IEnumerable<ProductoApi>>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task SyncAsync_UnchangedProduct_IsSkipped()
    {
        // Arrange
        var mockReader = new Mock<IViewReader>();
        var mockRepository = new Mock<IProductoRepository>();

        var view = new ProductoView
        {
            Code = "EXISTING-001",
            Nombre = "Producto Existente",
            PrecioNormal = 50,
            Inventario = 10
        };

        mockReader
            .Setup(r => r.ReadAsync(It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(view));

        var mapper = new ProductoMapperService(
            "https://img.test/",
            "https://img.test/default.png",
            "https://shop.test/{code}"
        );

        var mapped = mapper.MapAndValidate(view);
        string sameHash = mapped!.SourceHash;

        var bdHashMap = new Dictionary<string, string?>
        {
            { "EXISTING-001", sameHash }
        };

        mockRepository
            .Setup(r => r.LoadProductHashMapAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(bdHashMap);

        var service = new SyncService(mockReader.Object, mockRepository.Object, mapper);

        // Act
        var result = await service.SyncAsync(CancellationToken.None);

        // Assert
        Assert.Equal(0, result.NewProducts);
        Assert.Equal(0, result.UpdatedProducts);
        Assert.Equal(1, result.UnchangedProducts);

        mockRepository.Verify(
            r => r.UpsertProductsAsync(It.IsAny<IEnumerable<ProductoApi>>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task SyncAsync_ModifiedProduct_IsMarkedAsUpdated()
    {
        // Arrange
        var mockReader = new Mock<IViewReader>();
        var mockRepository = new Mock<IProductoRepository>();

        var view = new ProductoView
        {
            Code = "EXISTING-002",
            Nombre = "Producto Modificado",
            PrecioNormal = 75,
            Inventario = 5
        };

        mockReader
            .Setup(r => r.ReadAsync(It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(view));

        var bdHashMap = new Dictionary<string, string?>
        {
            { "EXISTING-002", "old_hash_value" }
        };

        mockRepository
            .Setup(r => r.LoadProductHashMapAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(bdHashMap);

        var mapper = new ProductoMapperService(
            "https://img.test/",
            "https://img.test/default.png",
            "https://shop.test/{code}"
        );

        var service = new SyncService(mockReader.Object, mockRepository.Object, mapper);

        // Act
        var result = await service.SyncAsync(CancellationToken.None);

        // Assert
        Assert.Equal(0, result.NewProducts);
        Assert.Equal(1, result.UpdatedProducts);
        Assert.Equal(0, result.UnchangedProducts);

        mockRepository.Verify(
            r => r.UpsertProductsAsync(It.IsAny<IEnumerable<ProductoApi>>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task SyncAsync_InvalidProduct_IsRejected()
    {
        // Arrange
        var mockReader = new Mock<IViewReader>();
        var mockRepository = new Mock<IProductoRepository>();

        var invalidView = new ProductoView
        {
            Code = "INVALID-001",
            Nombre = "Producto Inválido",
            PrecioNormal = 150000,
            Inventario = 10
        };

        mockReader
            .Setup(r => r.ReadAsync(It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(invalidView));

        mockRepository
            .Setup(r => r.LoadProductHashMapAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string?>());

        var mapper = new ProductoMapperService(
            "https://img.test/",
            "https://img.test/default.png",
            "https://shop.test/{code}"
        );

        var service = new SyncService(mockReader.Object, mockRepository.Object, mapper);

        // Act
        var result = await service.SyncAsync(CancellationToken.None);

        // Assert
        Assert.Equal(0, result.NewProducts);
        Assert.Equal(1, result.InvalidProducts);

        mockRepository.Verify(
            r => r.UpsertProductsAsync(It.IsAny<IEnumerable<ProductoApi>>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    private static async IAsyncEnumerable<ProductoView> CreateAsyncEnumerable(ProductoView view)
    {
        yield return view;
        await Task.CompletedTask;
    }
}

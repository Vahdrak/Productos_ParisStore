using ComprasParaguai.Core.Interfaces;
using ComprasParaguai.Core.Models;
using ComprasParaguai.Core.Services;
using Moq;
using Xunit;

namespace ComprasParaguai.UnitTests;

public class SendServiceTests
{
    [Fact]
    public async Task SendPendingAsync_SuccessfulBatch_MarksAsSent()
    {
        // Arrange
        var mockRepository = new Mock<IProductoRepository>();
        var mockClient = new Mock<IComprasParaguaiClient>();

        var product = new ProductoApi
        {
            Code = "TEST-001",
            NameEs = "Producto",
            NamePt = "Produto",
            Price = 50,
            Stock = 10,
            UrlImage = "https://img.test/img.png",
            Link = "https://shop.test/prod"
        };

        mockRepository
            .Setup(r => r.LoadPendingProductsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProductoApi> { product });

        var successResponse = new ImportResponse
        {
            Success = true,
            Message = "Import completed.",
            ProductsProcessed = 1,
            ProductsFailed = 0
        };

        mockClient
            .Setup(c => c.ImportProductsAsync(It.IsAny<IEnumerable<ProductoApi>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(successResponse);

        var service = new SendService(mockRepository.Object, mockClient.Object);

        // Act
        var result = await service.SendPendingAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, result.Sent);
        Assert.Equal(0, result.Failed);
        Assert.Empty(result.ErrorCodes);

        mockRepository.Verify(
            r => r.MarkAsSentAsync(
                It.Is<IEnumerable<string>>(c => c.Contains("TEST-001")),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task SendPendingAsync_FailedBatch_MarksAsError()
    {
        // Arrange
        var mockRepository = new Mock<IProductoRepository>();
        var mockClient = new Mock<IComprasParaguaiClient>();

        var product = new ProductoApi
        {
            Code = "INVALID-001",
            NameEs = "Producto Inválido",
            NamePt = "Produto Inválido",
            Price = 0.01m, // Precio mínimo inválido
            Stock = 10,
            UrlImage = "https://img.test/img.png",
            Link = "https://shop.test/prod"
        };

        mockRepository
            .Setup(r => r.LoadPendingProductsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProductoApi> { product });

        var failureResponse = new ImportResponse
        {
            Success = false,
            Message = "Validation failed",
            ProductsProcessed = 0,
            ProductsFailed = 1,
            ValidationErrors = new()
            {
                new ValidationError { Code = "INVALID-001", Field = "price", Error = "Price below minimum" }
            }
        };

        mockClient
            .Setup(c => c.ImportProductsAsync(It.IsAny<IEnumerable<ProductoApi>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(failureResponse);

        var service = new SendService(mockRepository.Object, mockClient.Object);

        // Act
        var result = await service.SendPendingAsync(CancellationToken.None);

        // Assert
        Assert.Equal(0, result.Sent);
        Assert.Equal(1, result.Failed);
        Assert.Single(result.ErrorCodes);
        Assert.Contains("INVALID-001", result.ErrorCodes);

        mockRepository.Verify(
            r => r.MarkAsErrorAsync(
                "INVALID-001",
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task SendPendingAsync_LargeBatch_SplitsIntoMultipleLotes()
    {
        // Arrange
        var mockRepository = new Mock<IProductoRepository>();
        var mockClient = new Mock<IComprasParaguaiClient>();

        // Crear 1100 productos (más de 500, requiere 3 lotes).
        var products = Enumerable.Range(1, 1100)
            .Select(i => new ProductoApi
            {
                Code = $"PROD-{i:D5}",
                NameEs = $"Producto {i}",
                NamePt = $"Produto {i}",
                Price = 50 + (i % 100),
                Stock = 10,
                UrlImage = "https://img.test/img.png",
                Link = $"https://shop.test/{i}"
            })
            .ToList();

        mockRepository
            .Setup(r => r.LoadPendingProductsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(products);

        var successResponse = new ImportResponse { Success = true, ProductsProcessed = 1 };

        mockClient
            .Setup(c => c.ImportProductsAsync(It.IsAny<IEnumerable<ProductoApi>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(successResponse);

        var service = new SendService(mockRepository.Object, mockClient.Object);

        // Act
        var result = await service.SendPendingAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1100, result.Sent);
        Assert.Equal(0, result.Failed);

        // Verificar que ImportProductsAsync fue llamada 3 veces (3 lotes).
        mockClient.Verify(
            c => c.ImportProductsAsync(It.IsAny<IEnumerable<ProductoApi>>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3)
        );
    }

    [Fact]
    public async Task SendPendingAsync_EmptyPending_ReturnsZero()
    {
        // Arrange
        var mockRepository = new Mock<IProductoRepository>();
        var mockClient = new Mock<IComprasParaguaiClient>();

        mockRepository
            .Setup(r => r.LoadPendingProductsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProductoApi>());

        var service = new SendService(mockRepository.Object, mockClient.Object);

        // Act
        var result = await service.SendPendingAsync(CancellationToken.None);

        // Assert
        Assert.Equal(0, result.Sent);
        Assert.Equal(0, result.Failed);
        Assert.Equal(0, result.Total);

        mockClient.Verify(
            c => c.ImportProductsAsync(It.IsAny<IEnumerable<ProductoApi>>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
}

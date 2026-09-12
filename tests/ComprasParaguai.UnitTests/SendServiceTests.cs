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
            .Setup(r => r.LoadPendingProductsAsync(It.IsAny<SyncTarget>(), It.IsAny<CancellationToken>()))
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

        var service = new SendService(mockRepository.Object, mockClient.Object, SendOptions.ComprasParaguai);

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
                It.IsAny<SyncTarget>(),
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
            .Setup(r => r.LoadPendingProductsAsync(It.IsAny<SyncTarget>(), It.IsAny<CancellationToken>()))
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

        var service = new SendService(mockRepository.Object, mockClient.Object, SendOptions.ComprasParaguai);

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
                It.IsAny<SyncTarget>(),
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
            .Setup(r => r.LoadPendingProductsAsync(It.IsAny<SyncTarget>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(products);

        var successResponse = new ImportResponse { Success = true, ProductsProcessed = 1 };

        mockClient
            .Setup(c => c.ImportProductsAsync(It.IsAny<IEnumerable<ProductoApi>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(successResponse);

        var service = new SendService(mockRepository.Object, mockClient.Object, SendOptions.ComprasParaguai);

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
    public async Task SendAllAsync_With40000Products_Sends80Batches()
    {
        // Arrange
        var mockRepository = new Mock<IProductoRepository>();
        var mockClient = new Mock<IComprasParaguaiClient>();

        // Crear 40000 productos (requiere 80 lotes de 500).
        var allProducts = Enumerable.Range(1, 40000)
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
            .Setup(r => r.LoadAllProductsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(allProducts);

        var successResponse = new ImportResponse { Success = true, ProductsProcessed = 1 };

        mockClient
            .Setup(c => c.ImportProductsAsync(It.IsAny<IEnumerable<ProductoApi>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(successResponse);

        var service = new SendService(mockRepository.Object, mockClient.Object, SendOptions.ComprasParaguai);

        // Act
        var result = await service.SendAllAsync(CancellationToken.None);

        // Assert
        Assert.Equal(40000, result.Sent);
        Assert.Equal(0, result.Failed);
        Assert.Equal(40000, result.Total);

        // Verificar que ImportProductsAsync fue llamada 80 veces.
        mockClient.Verify(
            c => c.ImportProductsAsync(It.IsAny<IEnumerable<ProductoApi>>(), It.IsAny<CancellationToken>()),
            Times.Exactly(80)
        );
    }

    [Fact]
    public async Task SendAllAsync_SendsAllProducts_InBatches()
    {
        // Arrange
        var mockRepository = new Mock<IProductoRepository>();
        var mockClient = new Mock<IComprasParaguaiClient>();

        // Crear 2000 productos (requiere 4 lotes de 500).
        var allProducts = Enumerable.Range(1, 2000)
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
            .Setup(r => r.LoadAllProductsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(allProducts);

        var successResponse = new ImportResponse { Success = true, ProductsProcessed = 1 };

        mockClient
            .Setup(c => c.ImportProductsAsync(It.IsAny<IEnumerable<ProductoApi>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(successResponse);

        var service = new SendService(mockRepository.Object, mockClient.Object, SendOptions.ComprasParaguai);

        // Act
        var result = await service.SendAllAsync(CancellationToken.None);

        // Assert
        Assert.Equal(2000, result.Sent);
        Assert.Equal(0, result.Failed);
        Assert.Equal(2000, result.Total);

        // Verificar que ImportProductsAsync fue llamada 4 veces (4 lotes).
        mockClient.Verify(
            c => c.ImportProductsAsync(It.IsAny<IEnumerable<ProductoApi>>(), It.IsAny<CancellationToken>()),
            Times.Exactly(4)
        );

        // Verificar que MarkAsSentAsync fue llamada 4 veces (una por cada lote).
        mockRepository.Verify(
            r => r.MarkAsSentAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<DateTime>(), It.IsAny<SyncTarget>(), It.IsAny<CancellationToken>()),
            Times.Exactly(4)
        );
    }

    [Fact]
    public async Task SendAllAsync_EmptyAll_ReturnsZero()
    {
        // Arrange
        var mockRepository = new Mock<IProductoRepository>();
        var mockClient = new Mock<IComprasParaguaiClient>();

        mockRepository
            .Setup(r => r.LoadAllProductsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProductoApi>());

        var service = new SendService(mockRepository.Object, mockClient.Object, SendOptions.ComprasParaguai);

        // Act
        var result = await service.SendAllAsync(CancellationToken.None);

        // Assert
        Assert.Equal(0, result.Sent);
        Assert.Equal(0, result.Failed);
        Assert.Equal(0, result.Total);

        mockClient.Verify(
            c => c.ImportProductsAsync(It.IsAny<IEnumerable<ProductoApi>>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task SendPendingAsync_AfterSendAll_OnlyFetchesChanges()
    {
        // Arrange
        var mockRepository = new Mock<IProductoRepository>();
        var mockClient = new Mock<IComprasParaguaiClient>();

        // Simular que después de SendAll, solo hay algunos cambios pendientes.
        var pendingProducts = Enumerable.Range(1, 150)
            .Select(i => new ProductoApi
            {
                Code = $"PROD-CHANGED-{i:D3}",
                NameEs = $"Producto Modificado {i}",
                NamePt = $"Produto Modificado {i}",
                Price = 99.99m,
                Stock = 5,
                UrlImage = "https://img.test/changed.png",
                Link = $"https://shop.test/changed/{i}"
            })
            .ToList();

        mockRepository
            .Setup(r => r.LoadPendingProductsAsync(It.IsAny<SyncTarget>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pendingProducts);

        var successResponse = new ImportResponse { Success = true, ProductsProcessed = 1 };

        mockClient
            .Setup(c => c.ImportProductsAsync(It.IsAny<IEnumerable<ProductoApi>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(successResponse);

        var service = new SendService(mockRepository.Object, mockClient.Object, SendOptions.ComprasParaguai);

        // Act
        var result = await service.SendPendingAsync(CancellationToken.None);

        // Assert
        Assert.Equal(150, result.Sent);
        Assert.Equal(0, result.Failed);
        Assert.Equal(150, result.Total);

        // Verificar que ImportProductsAsync fue llamada 1 vez (150 caben en 1 lote de 500).
        mockClient.Verify(
            c => c.ImportProductsAsync(It.IsAny<IEnumerable<ProductoApi>>(), It.IsAny<CancellationToken>()),
            Times.Once
        );

        // Verificar que MarkAsSentAsync fue llamada 1 vez.
        mockRepository.Verify(
            r => r.MarkAsSentAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<DateTime>(), It.IsAny<SyncTarget>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task SendPendingAsync_EmptyPending_ReturnsZero()
    {
        // Arrange
        var mockRepository = new Mock<IProductoRepository>();
        var mockClient = new Mock<IComprasParaguaiClient>();

        mockRepository
            .Setup(r => r.LoadPendingProductsAsync(It.IsAny<SyncTarget>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProductoApi>());

        var service = new SendService(mockRepository.Object, mockClient.Object, SendOptions.ComprasParaguai);

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

    [Fact]
    public async Task SendPendingAsync_ComparaTarget_ReadsAndWritesOnlyComparaColumns()
    {
        var mockRepository = new Mock<IProductoRepository>();
        var mockClient = new Mock<IComprasParaguaiClient>();

        var ok = new ProductoApi { Code = "OK-1", NamePt = "x", UrlImage = "u", Link = "l", Price = 10, Stock = 1 };
        var bad = new ProductoApi { Code = "BAD-1", NamePt = "x", UrlImage = "u", Link = "l", Price = 10, Stock = 1 };

        mockRepository
            .Setup(r => r.LoadPendingProductsAsync(SyncTarget.ComparaParaguai, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProductoApi> { ok, bad });

        mockClient
            .SetupSequence(c => c.ImportProductsAsync(It.IsAny<IEnumerable<ProductoApi>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImportResponse { Success = true })
            .ReturnsAsync(new ImportResponse { Success = false, Message = "boom" });

        // BatchSize 1 para que cada producto sea su propio lote.
        var service = new SendService(mockRepository.Object, mockClient.Object,
            new SendOptions(SyncTarget.ComparaParaguai, BatchSize: 1, DelayMs: 0));

        var result = await service.SendPendingAsync(CancellationToken.None);

        Assert.Equal(1, result.Sent);
        Assert.Equal(1, result.Failed);

        mockRepository.Verify(r => r.MarkAsSentAsync(
            It.Is<IEnumerable<string>>(c => c.Contains("OK-1")), It.IsAny<DateTime>(),
            SyncTarget.ComparaParaguai, It.IsAny<CancellationToken>()), Times.Once);
        mockRepository.Verify(r => r.MarkAsErrorAsync(
            "BAD-1", "boom", SyncTarget.ComparaParaguai, It.IsAny<CancellationToken>()), Times.Once);

        // Nunca toca la columna de Compras.
        mockRepository.Verify(r => r.LoadPendingProductsAsync(SyncTarget.ComprasParaguai, It.IsAny<CancellationToken>()), Times.Never);
        mockRepository.Verify(r => r.MarkAsSentAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<DateTime>(),
            SyncTarget.ComprasParaguai, It.IsAny<CancellationToken>()), Times.Never);
        mockRepository.Verify(r => r.MarkAsErrorAsync(It.IsAny<string>(), It.IsAny<string>(),
            SyncTarget.ComprasParaguai, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendSampleAsync_LoadsRandomAndSends()
    {
        var mockRepository = new Mock<IProductoRepository>();
        var mockClient = new Mock<IComprasParaguaiClient>();

        var sample = Enumerable.Range(1, 500)
            .Select(i => new ProductoApi { Code = $"R-{i}", NamePt = "x", UrlImage = "u", Link = "l", Price = 10, Stock = 1 })
            .ToList();

        mockRepository
            .Setup(r => r.LoadRandomProductsAsync(500, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sample);
        mockClient
            .Setup(c => c.ImportProductsAsync(It.IsAny<IEnumerable<ProductoApi>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImportResponse { Success = true });

        var service = new SendService(mockRepository.Object, mockClient.Object, SendOptions.ComparaParaguai);

        var result = await service.SendSampleAsync(500, CancellationToken.None);

        Assert.Equal(500, result.Sent);
        Assert.Equal(500, result.Total);
        mockRepository.Verify(r => r.LoadAllProductsAsync(It.IsAny<CancellationToken>()), Times.Never);
        mockRepository.Verify(r => r.LoadPendingProductsAsync(It.IsAny<SyncTarget>(), It.IsAny<CancellationToken>()), Times.Never);
        mockClient.Verify(c => c.ImportProductsAsync(It.IsAny<IEnumerable<ProductoApi>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void SendOptions_Defaults_MatchClientLimits()
    {
        Assert.Equal(500, SendOptions.ComprasParaguai.BatchSize);
        Assert.Equal(SyncTarget.ComprasParaguai, SendOptions.ComprasParaguai.Target);
        Assert.Equal(1000, SendOptions.ComparaParaguai.BatchSize);
        Assert.Equal(SyncTarget.ComparaParaguai, SendOptions.ComparaParaguai.Target);
        Assert.True(SendOptions.ComparaParaguai.DelayMs > 0);
    }
}

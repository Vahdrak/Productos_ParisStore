using ComprasParaguai.Core.Interfaces;
using ComprasParaguai.Core.Models;
using ComprasParaguai.Core.Services;
using Moq;
using Xunit;

namespace ComprasParaguai.UnitTests;

public class VerifyServiceTests
{
    [Fact]
    public async Task VerifyAsync_ReportsFoundAndMissing_AndComparesPriceStock()
    {
        var repo = new Mock<IProductoRepository>();
        var client = new Mock<IProductQueryClient>();

        var a = new ProductoApi { Code = "A", NamePt = "a", Price = 10m, Stock = 5, UrlImage = "u", Link = "l" };
        var b = new ProductoApi { Code = "B", NamePt = "b", Price = 20m, Stock = 1, UrlImage = "u", Link = "l" };
        var c = new ProductoApi { Code = "C", NamePt = "c", Price = 30m, Stock = 0, UrlImage = "u", Link = "l" };

        repo.Setup(r => r.LoadRandomSentProductsAsync(3, SyncTarget.ComprasParaguai, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProductoApi> { a, b, c });

        client.Setup(x => x.GetByCodeAsync("A", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RemoteProduct("A", "a", 10m, 11m, 5m, true, "l", DateTime.UtcNow));
        client.Setup(x => x.GetByCodeAsync("B", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RemoteProduct("B", "b", 19m, null, 1m, true, "l", null)); // precio distinto
        client.Setup(x => x.GetByCodeAsync("C", It.IsAny<CancellationToken>()))
            .ReturnsAsync((RemoteProduct?)null); // no está en el destino

        var service = new VerifyService(repo.Object, client.Object, SyncTarget.ComprasParaguai);

        var results = await service.VerifyAsync(3, delayMs: 0);

        Assert.Equal(3, results.Count);
        Assert.True(results[0].Found); Assert.True(results[0].PriceMatch); Assert.True(results[0].StockMatch);
        Assert.True(results[1].Found); Assert.False(results[1].PriceMatch); Assert.True(results[1].StockMatch);
        Assert.False(results[2].Found); Assert.False(results[2].PriceMatch);

        // Solo consulta los Enviados del target pedido, nunca pendientes ni todos.
        repo.Verify(r => r.LoadPendingProductsAsync(It.IsAny<SyncTarget>(), It.IsAny<CancellationToken>()), Times.Never);
        repo.Verify(r => r.LoadAllProductsAsync(It.IsAny<SyncTarget>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

using ComprasParaguai.Core.Interfaces;
using ComprasParaguai.Core.Models;
using ComprasParaguai.Core.Services;
using Moq;
using Xunit;

namespace ComprasParaguai.UnitTests;

/// <summary>Retiro del destino (stock 0) y liberación de productos al pausar / reactivar envíos.</summary>
public class SendRetiroTests
{
    private static ProductoApi Producto(string code, int stock = 10) => new()
    {
        Code = code, NameEs = "P", NamePt = "P", Price = 10, Stock = stock,
        UrlImage = "https://img.test/x.png", Link = "https://shop.test/x"
    };

    private static ImportResponse Ok() => new() { Success = true, Message = "ok", ProductsProcessed = 1, ProductsFailed = 0 };

    [Fact]
    public async Task SendPendingAsync_RetiraConStockCero_YLoRegistraComoRetirado_SinMarcarEnviado()
    {
        var repo = new Mock<IProductoRepository>();
        var client = new Mock<IComprasParaguaiClient>();
        var enviados = new List<int>();

        repo.Setup(r => r.LoadParaRetirarAsync(It.IsAny<SyncTarget>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProductoApi> { Producto("RET-1", stock: 25) });
        repo.Setup(r => r.LoadPendingProductsAsync(It.IsAny<SyncTarget>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProductoApi>());
        client.Setup(c => c.ImportProductsAsync(It.IsAny<IEnumerable<ProductoApi>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<ProductoApi>, CancellationToken>((lote, _) => enviados.AddRange(lote.Select(p => p.Stock)))
            .ReturnsAsync(Ok());

        await new SendService(repo.Object, client.Object, SendOptions.ComparaParaguai with { DelayMs = 0 }).SendPendingAsync();

        Assert.Equal(new[] { 0 }, enviados);   // salió con stock 0
        repo.Verify(r => r.MarkRetiradosAsync(It.Is<IEnumerable<string>>(c => c.Single() == "RET-1"),
            SyncTarget.ComparaParaguai, It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.MarkAsSentAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<DateTime>(),
            It.IsAny<SyncTarget>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendPendingAsync_LiberaRetiradosAntesDeCargarPendientes()
    {
        var repo = new Mock<IProductoRepository>();
        var client = new Mock<IComprasParaguaiClient>();
        var orden = new List<string>();

        repo.Setup(r => r.LiberarRetiradosAsync(It.IsAny<SyncTarget>(), It.IsAny<CancellationToken>()))
            .Callback(() => orden.Add("liberar")).ReturnsAsync(3);
        repo.Setup(r => r.LoadParaRetirarAsync(It.IsAny<SyncTarget>(), It.IsAny<CancellationToken>()))
            .Callback(() => orden.Add("retirar")).ReturnsAsync(new List<ProductoApi>());
        repo.Setup(r => r.LoadPendingProductsAsync(It.IsAny<SyncTarget>(), It.IsAny<CancellationToken>()))
            .Callback(() => orden.Add("pendientes")).ReturnsAsync(new List<ProductoApi>());

        await new SendService(repo.Object, client.Object, SendOptions.ComprasParaguai).SendPendingAsync();

        Assert.Equal(new[] { "liberar", "retirar", "pendientes" }, orden);
        client.Verify(c => c.ImportProductsAsync(It.IsAny<IEnumerable<ProductoApi>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendPendingAsync_SinNadaQueRetirar_NoLlamaAlClienteNiCreaCorridaDeRetiro()
    {
        var repo = new Mock<IProductoRepository>();
        var client = new Mock<IComprasParaguaiClient>();
        var runs = new Mock<ISendRunRepository>();
        runs.Setup(r => r.StartAsync(It.IsAny<SyncTarget>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(1L);
        repo.Setup(r => r.LoadParaRetirarAsync(It.IsAny<SyncTarget>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<ProductoApi>());
        repo.Setup(r => r.LoadPendingProductsAsync(It.IsAny<SyncTarget>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<ProductoApi>());

        await new SendService(repo.Object, client.Object, SendOptions.ComprasParaguai, runs.Object).SendPendingAsync();

        runs.Verify(r => r.StartAsync(It.IsAny<SyncTarget>(), "retiro", It.IsAny<CancellationToken>()), Times.Never);
        runs.Verify(r => r.StartAsync(It.IsAny<SyncTarget>(), "pending", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAllAsync_YSendSampleAsync_PidenSoloLosEnviablesDelDestino()
    {
        var repo = new Mock<IProductoRepository>();
        var client = new Mock<IComprasParaguaiClient>();
        repo.Setup(r => r.LoadAllProductsAsync(It.IsAny<SyncTarget>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<ProductoApi>());
        repo.Setup(r => r.LoadRandomProductsAsync(It.IsAny<int>(), It.IsAny<SyncTarget>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<ProductoApi>());
        var service = new SendService(repo.Object, client.Object, SendOptions.ComparaParaguai);

        await service.SendAllAsync();
        await service.SendSampleAsync(5);

        repo.Verify(r => r.LoadAllProductsAsync(SyncTarget.ComparaParaguai, It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.LoadRandomProductsAsync(5, SyncTarget.ComparaParaguai, It.IsAny<CancellationToken>()), Times.Once);
    }
}

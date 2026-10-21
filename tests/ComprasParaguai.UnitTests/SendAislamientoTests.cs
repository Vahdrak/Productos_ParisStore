using ComprasParaguai.Core.Interfaces;
using ComprasParaguai.Core.Models;
using ComprasParaguai.Core.Services;
using Moq;
using Xunit;

namespace ComprasParaguai.UnitTests;

/// <summary>
/// Un 5xx de todo el lote suele ser UN producto que el destino no procesa. Se parte el lote en mitades para aislarlo:
/// solo ese producto queda en Error y el resto se envía.
/// </summary>
public class SendAislamientoTests
{
    private static ProductoApi P(string code) => new()
    {
        Code = code, NameEs = "P", NamePt = "P", Price = 10, Stock = 1,
        UrlImage = "https://img.test/x.png", Link = "https://shop.test/x"
    };

    private static ImportResponse Ok() => new() { Success = true, Message = "ok" };

    private static ImportResponse Error500() => new()
    {
        Success = false, StatusCode = 500,
        Message = "HTTP InternalServerError: {\"errors\":\"Duplicate entry 'x' for key 'pesquisa_produto.slug'\"}"
    };

    /// <summary>Cliente falso que responde 500 a cualquier lote que contenga alguno de los códigos "malos".</summary>
    private static (Mock<IComprasParaguaiClient> Client, List<string[]> Lotes) ClienteConVenenosos(params string[] malos)
    {
        var lotes = new List<string[]>();
        var client = new Mock<IComprasParaguaiClient>();
        client.Setup(c => c.ImportProductsAsync(It.IsAny<IEnumerable<ProductoApi>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<ProductoApi> lote, CancellationToken _) =>
            {
                var codes = lote.Select(p => p.Code).ToArray();
                lotes.Add(codes);
                return codes.Any(malos.Contains) ? Error500() : Ok();
            });
        return (client, lotes);
    }

    private static Mock<IProductoRepository> RepoCon(IEnumerable<ProductoApi> pendientes)
    {
        var repo = new Mock<IProductoRepository>();
        repo.Setup(r => r.LoadPendingProductsAsync(It.IsAny<SyncTarget>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pendientes.ToList());
        return repo;
    }

    [Fact]
    public async Task LoteConUnVenenoso_SoloEseProductoQuedaEnError_YElRestoSeEnvia()
    {
        var productos = Enumerable.Range(1, 8).Select(i => P($"P{i}")).ToList();
        var repo = RepoCon(productos);
        var (client, lotes) = ClienteConVenenosos("P3");
        var enviados = new List<string>();
        repo.Setup(r => r.MarkAsSentAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<DateTime>(), It.IsAny<SyncTarget>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<string>, DateTime, SyncTarget, CancellationToken>((c, _, _, _) => enviados.AddRange(c));

        var result = await new SendService(repo.Object, client.Object, SendOptions.ComprasParaguai).SendPendingAsync();

        Assert.Equal(7, result.Sent);
        Assert.Equal(1, result.Failed);
        Assert.Equal(new[] { "P3" }, result.ErrorCodes);
        Assert.Equal(7, enviados.Count);
        Assert.DoesNotContain("P3", enviados);
        repo.Verify(r => r.MarkAsErrorAsync("P3", It.Is<string>(m => m.Contains("Duplicate entry")), It.IsAny<SyncTarget>(), It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.MarkAsErrorAsync(It.Is<string>(c => c != "P3"), It.IsAny<string>(), It.IsAny<SyncTarget>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.True(lotes.Count < 2 * 8, "no debe enviar producto por producto");
    }

    [Fact]
    public async Task LoteConDosVenenosos_AislaLosDos()
    {
        var productos = Enumerable.Range(1, 16).Select(i => P($"P{i}")).ToList();
        var repo = RepoCon(productos);
        var (client, _) = ClienteConVenenosos("P2", "P13");

        var result = await new SendService(repo.Object, client.Object, SendOptions.ComprasParaguai).SendPendingAsync();

        Assert.Equal(14, result.Sent);
        Assert.Equal(new[] { "P2", "P13" }.OrderBy(x => x), result.ErrorCodes.OrderBy(x => x));
    }

    [Fact]
    public async Task ErrorDeCliente4xx_NoSeParte_SeMarcaTodoElLote()
    {
        var productos = Enumerable.Range(1, 4).Select(i => P($"P{i}")).ToList();
        var repo = RepoCon(productos);
        var client = new Mock<IComprasParaguaiClient>();
        client.Setup(c => c.ImportProductsAsync(It.IsAny<IEnumerable<ProductoApi>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImportResponse { Success = false, StatusCode = 401, Message = "HTTP Unauthorized" });

        var result = await new SendService(repo.Object, client.Object, SendOptions.ComprasParaguai).SendPendingAsync();

        Assert.Equal(0, result.Sent);
        Assert.Equal(4, result.Failed);
        client.Verify(c => c.ImportProductsAsync(It.IsAny<IEnumerable<ProductoApi>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ServidorCaido_TodosFallan_YLosPedidosExtraTienenTope()
    {
        var productos = Enumerable.Range(1, 500).Select(i => P($"P{i}")).ToList();
        var repo = RepoCon(productos);
        var client = new Mock<IComprasParaguaiClient>();
        client.Setup(c => c.ImportProductsAsync(It.IsAny<IEnumerable<ProductoApi>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Error500());

        var result = await new SendService(repo.Object, client.Object, SendOptions.ComprasParaguai).SendPendingAsync();

        Assert.Equal(0, result.Sent);
        Assert.Equal(500, result.Failed);
        // 1 pedido inicial + a lo sumo 60 extra (más el que cruza el tope): nunca un pedido por producto.
        client.Verify(c => c.ImportProductsAsync(It.IsAny<IEnumerable<ProductoApi>>(), It.IsAny<CancellationToken>()), Times.AtMost(70));
    }

    [Fact]
    public async Task Retiro_TambienAislaAlVenenoso()
    {
        var repo = new Mock<IProductoRepository>();
        repo.Setup(r => r.LoadParaRetirarAsync(It.IsAny<SyncTarget>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Range(1, 4).Select(i => P($"R{i}")).ToList());
        repo.Setup(r => r.LoadPendingProductsAsync(It.IsAny<SyncTarget>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<ProductoApi>());
        var (client, _) = ClienteConVenenosos("R2");
        var retirados = new List<string>();
        repo.Setup(r => r.MarkRetiradosAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<SyncTarget>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<string>, SyncTarget, CancellationToken>((c, _, _) => retirados.AddRange(c));

        await new SendService(repo.Object, client.Object, SendOptions.ComprasParaguai).SendPendingAsync();

        Assert.Equal(new[] { "R1", "R3", "R4" }, retirados.OrderBy(x => x));
        repo.Verify(r => r.MarkRetiroErrorAsync("R2", It.Is<string>(m => m.Contains("Duplicate entry")), It.IsAny<SyncTarget>(), It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.MarkAsErrorAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SyncTarget>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

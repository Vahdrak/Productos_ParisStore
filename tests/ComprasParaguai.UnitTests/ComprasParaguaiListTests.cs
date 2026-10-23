using System.Net;
using System.Text;
using ComprasParaguai.Infrastructure.Clients;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ComprasParaguai.UnitTests;

public class ComprasParaguaiListTests
{
    // Responde según la URL pedida y registra cada petición.
    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<string, (HttpStatusCode, string)> _respond;
        public List<string> Requests { get; } = new();

        public FakeHandler(Func<string, (HttpStatusCode, string)> respond) => _respond = respond;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.PathAndQuery;
            Requests.Add(url);
            var (status, body) = _respond(url);
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    private static ComprasParaguaiHttpClient CreateClient(FakeHandler handler) =>
        new(new HttpClient(handler), "https://compras.test", "token-de-prueba", NullLogger<ComprasParaguaiHttpClient>.Instance);

    private static string Page(bool hasNext, int total, params (string code, double stock, bool available)[] items) =>
        "{\"data\":[" + string.Join(",", items.Select(i =>
            $"{{\"code\":\"{i.code}\",\"name_pt\":\"P {i.code}\",\"price\":10.5,\"price_iva\":11.0,\"stock\":{i.stock.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
            $"\"available\":{(i.available ? "true" : "false")},\"link\":\"https://tienda/{i.code}\",\"updated_at\":\"2026-10-22T16:55:47\"}}")) +
        $"],\"total_items\":{total},\"has_next\":{(hasNext ? "true" : "false")}}}";

    [Fact]
    public async Task ListAsync_FollowsHasNext_AndSendsFilters()
    {
        var handler = new FakeHandler(url => url.Contains("page=1")
            ? (HttpStatusCode.OK, Page(true, 3, ("A", 2, false), ("B", 5, false)))
            : (HttpStatusCode.OK, Page(false, 3, ("C", 1, false))));

        var items = await CreateClient(handler).ListAsync(available: false, withStock: true, delayMs: 0);

        Assert.Equal(new[] { "A", "B", "C" }, items.Select(i => i.Code));
        Assert.All(items, i => Assert.False(i.Available));
        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, r =>
        {
            Assert.Contains("available=false", r);
            Assert.Contains("with_stock=true", r);
        });
    }

    [Fact]
    public async Task ListAsync_OmitsFiltersThatAreNull()
    {
        var handler = new FakeHandler(_ => (HttpStatusCode.OK, Page(false, 1, ("A", 1, true))));

        await CreateClient(handler).ListAsync(available: null, withStock: null, delayMs: 0);

        var url = Assert.Single(handler.Requests);
        Assert.DoesNotContain("available", url);
        Assert.DoesNotContain("with_stock", url);
    }

    [Fact]
    public async Task ListAsync_ConvertsParaguayTimeToUtc()
    {
        var handler = new FakeHandler(_ => (HttpStatusCode.OK, Page(false, 1, ("A", 1, false))));

        var item = Assert.Single(await CreateClient(handler).ListAsync(false, true, 0));

        // 16:55:47 en Paraguay (UTC-3) = 19:55:47 UTC.
        Assert.Equal(new DateTime(2026, 10, 22, 19, 55, 47, DateTimeKind.Utc), item.UpdatedAt);
    }

    [Fact]
    public async Task CountAsync_ReturnsTotalItems()
    {
        var handler = new FakeHandler(_ => (HttpStatusCode.OK, Page(true, 47537, ("A", 1, true))));

        Assert.Equal(47537, await CreateClient(handler).CountAsync(null, true));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ListAsync_ThrowsOnHttpError()
    {
        var handler = new FakeHandler(_ => (HttpStatusCode.Forbidden, "{\"detail\":\"no\"}"));

        await Assert.ThrowsAsync<HttpRequestException>(() => CreateClient(handler).ListAsync(false, true, 0));
    }
}

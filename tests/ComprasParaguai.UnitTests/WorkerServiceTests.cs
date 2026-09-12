using ComprasParaguai.Core.Interfaces;
using ComprasParaguai.Core.Services;
using Moq;
using Xunit;

namespace ComprasParaguai.UnitTests;

public class WorkerServiceTests
{
    [Fact]
    public async Task ExecuteSyncAsync_RunsSyncAndLogsRun_WithoutSending()
    {
        var syncService = new Mock<ISyncService>();
        var syncRuns = new Mock<ISyncRunRepository>();

        syncRuns.Setup(r => r.StartSyncAsync(It.IsAny<CancellationToken>())).ReturnsAsync(42L);
        syncService.Setup(s => s.SyncAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncResult(NewProducts: 3, UpdatedProducts: 2, UnchangedProducts: 10, InvalidProducts: 1, TotalRows: 16));

        var worker = new WorkerService(syncService.Object, syncRuns.Object);

        await worker.ExecuteSyncAsync(CancellationToken.None);

        syncService.Verify(s => s.SyncAsync(It.IsAny<CancellationToken>()), Times.Once);
        syncRuns.Verify(r => r.FinishSyncAsync(
            42L,
            It.Is<SyncResult>(x => x.NewProducts == 3 && x.UpdatedProducts == 2),
            It.Is<SendResult>(x => x.Sent == 0 && x.Failed == 0 && x.Total == 0),
            It.Is<string>(n => n.Contains("3 nuevos") && n.Contains("2 actualizados")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteSyncAsync_WhenSyncThrows_FinishesRunWithErrorAndRethrows()
    {
        var syncService = new Mock<ISyncService>();
        var syncRuns = new Mock<ISyncRunRepository>();

        syncRuns.Setup(r => r.StartSyncAsync(It.IsAny<CancellationToken>())).ReturnsAsync(7L);
        syncService.Setup(s => s.SyncAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("oracle down"));

        var worker = new WorkerService(syncService.Object, syncRuns.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => worker.ExecuteSyncAsync(CancellationToken.None));

        syncRuns.Verify(r => r.FinishSyncAsync(
            7L, It.IsAny<SyncResult>(), It.IsAny<SendResult>(),
            It.Is<string>(n => n.Contains("oracle down")),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}

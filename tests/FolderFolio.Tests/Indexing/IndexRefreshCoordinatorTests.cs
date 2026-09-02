using FolderFolio.Domain;
using FolderFolio.Indexing;
using FolderFolio.Tests.Support;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace FolderFolio.Tests.Indexing;

public sealed class IndexRefreshCoordinatorTests
{
    [Fact]
    public async Task Debounces_and_deduplicates_album_requests_using_the_supplied_clock()
    {
        var queue = new IndexRefreshQueue();
        var scanner = new StubPhotoScanner();
        var index = new PortfolioIndex();
        var timeProvider = new FakeTimeProvider();
        var coordinator = new IndexRefreshCoordinator(queue, scanner, index, timeProvider);
        queue.RequestAlbum("01-Landscapes");
        queue.RequestAlbum("01-Landscapes");
        queue.RequestAlbum("01-Landscapes");

        var scan = coordinator.ProcessNextBatchAsync(TestContext.Current.CancellationToken);
        timeProvider.Advance(TimeSpan.FromMilliseconds(750));
        await scan;

        Assert.Equal(1, scanner.RescanAlbumsCallCount);
        Assert.Equal(["01-Landscapes"], scanner.LastAlbumDirectoryNames);
        Assert.Equal(IndexStatus.Ready, index.Current.Status);
    }

    [Fact]
    public async Task A_full_scan_request_wins_over_targeted_requests()
    {
        var queue = new IndexRefreshQueue();
        var scanner = new StubPhotoScanner();
        var index = new PortfolioIndex();
        var timeProvider = new FakeTimeProvider();
        var coordinator = new IndexRefreshCoordinator(queue, scanner, index, timeProvider);
        queue.RequestAlbum("01-Landscapes");
        queue.RequestFullScan();

        var scan = coordinator.ProcessNextBatchAsync(TestContext.Current.CancellationToken);
        timeProvider.Advance(TimeSpan.FromMilliseconds(750));
        await scan;

        Assert.Equal(1, scanner.ScanAllCallCount);
        Assert.Equal(0, scanner.RescanAlbumsCallCount);
    }

    [Fact]
    public async Task A_failed_scan_degrades_without_losing_the_last_ready_snapshot()
    {
        var snapshot = PortfolioSnapshot.Empty;
        var queue = new IndexRefreshQueue();
        var scanner = new StubPhotoScanner { AllResult = new PhotoScanResult(snapshot, 0) };
        var index = new PortfolioIndex();
        var timeProvider = new FakeTimeProvider();
        var coordinator = new IndexRefreshCoordinator(queue, scanner, index, timeProvider);
        queue.RequestFullScan();
        var firstScan = coordinator.ProcessNextBatchAsync(TestContext.Current.CancellationToken);
        timeProvider.Advance(TimeSpan.FromMilliseconds(750));
        await firstScan;
        scanner.Exception = new IOException("not public");
        queue.RequestFullScan();

        var failedScan = coordinator.ProcessNextBatchAsync(TestContext.Current.CancellationToken);
        timeProvider.Advance(TimeSpan.FromMilliseconds(750));
        await Assert.ThrowsAsync<IOException>(() => failedScan);

        Assert.Equal(IndexStatus.Degraded, index.Current.Status);
        Assert.Same(snapshot, index.Current.Snapshot);
    }
}

using FolderFolio.Configuration;
using FolderFolio.Domain;
using FolderFolio.Indexing;
using FolderFolio.Tests.Support;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace FolderFolio.Tests.Indexing;

public sealed class IndexingServiceTests
{
    [Fact]
    public async Task An_unavailable_root_is_retried_before_the_initial_scan()
    {
        using var directory = new TemporaryDirectory();
        var photoRoot = Path.Combine(directory.Path, "not-created-yet");
        var options = new FolderFolioOptions { PhotoRoot = photoRoot, CacheRoot = directory.Path };
        var queue = new IndexRefreshQueue();
        var index = new PortfolioIndex();
        var clock = new FakeTimeProvider();
        var scanner = new StubPhotoScanner();
        var watcher = new RecordingPhotoRootWatcher();
        using var service = new IndexingService(
            options,
            queue,
            new IndexRefreshCoordinator(queue, scanner, index, clock),
            index,
            watcher,
            clock);

        await service.StartAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => index.Current.PublicError == "Photo root is unavailable.", TestContext.Current.CancellationToken);
        Assert.Equal(0, scanner.ScanAllCallCount);
        Assert.False(watcher.Started);

        Directory.CreateDirectory(photoRoot);
        clock.Advance(TimeSpan.FromSeconds(5));
        await WaitUntilAsync(() => index.Current.Status == IndexStatus.Ready, TestContext.Current.CancellationToken);

        Assert.Equal(1, scanner.ScanAllCallCount);
        await service.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task An_initial_scan_failure_remains_starting_and_retries_after_starting_the_watcher()
    {
        using var directory = new TemporaryDirectory();
        var options = new FolderFolioOptions { PhotoRoot = directory.Path, CacheRoot = directory.Path };
        var queue = new IndexRefreshQueue();
        var index = new PortfolioIndex();
        var scanner = new StubPhotoScanner { Exception = new IOException("cannot scan") };
        var watcher = new RecordingPhotoRootWatcher();
        scanner.OnScanAll = () => watcher.WasStartedWhenInitialScanRan = watcher.Started;
        using var service = new IndexingService(
            options,
            queue,
            new IndexRefreshCoordinator(queue, scanner, index, TimeProvider.System),
            index,
            watcher,
            TimeProvider.System);

        await service.StartAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => scanner.ScanAllCallCount == 1, TestContext.Current.CancellationToken);

        Assert.Equal(IndexStatus.Starting, index.Current.Status);
        Assert.True(watcher.WasStartedWhenInitialScanRan);
        await service.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_root_that_disappears_after_a_successful_scan_degrades_and_retains_the_last_snapshot()
    {
        using var directory = new TemporaryDirectory();
        var album = directory.CreateDirectory("01-Trip");
        ImageFixtureFactory.CreateJpeg(Path.Combine(album, "before.jpg"));
        var options = new FolderFolioOptions { PhotoRoot = directory.Path, CacheRoot = directory.Path };
        var queue = new IndexRefreshQueue();
        var index = new PortfolioIndex();
        var coordinator = new IndexRefreshCoordinator(
            queue,
            new PhotoScanner(options, new ImageSharpMetadataReader()),
            index,
            TimeProvider.System);
        using var service = new IndexingService(
            options,
            queue,
            coordinator,
            index,
            new RecordingPhotoRootWatcher(),
            TimeProvider.System);

        await service.StartAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => index.Current.Status == IndexStatus.Ready, TestContext.Current.CancellationToken);
        var lastSuccessfulSnapshot = index.Current.Snapshot;

        Directory.Delete(directory.Path, recursive: true);
        queue.RequestFullScan();

        await WaitUntilAsync(() => index.Current.Status == IndexStatus.Degraded, TestContext.Current.CancellationToken);

        Assert.Same(lastSuccessfulSnapshot, index.Current.Snapshot);
        await service.StopAsync(TestContext.Current.CancellationToken);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(4);
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException("The expected indexing lifecycle state was not observed.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken);
        }
    }

    private sealed class RecordingPhotoRootWatcher : IPhotoRootWatcher
    {
        public bool Started { get; private set; }

        public bool WasStartedWhenInitialScanRan { get; set; }

        public void Start() => Started = true;

        public void Dispose() { }
    }
}

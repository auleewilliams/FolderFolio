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
    public async Task A_root_that_disappears_after_a_successful_scan_remains_degraded_through_the_retry_loop()
    {
        using var directory = new TemporaryDirectory();
        var album = directory.CreateDirectory("01-Trip");
        ImageFixtureFactory.CreateJpeg(Path.Combine(album, "before.jpg"));
        var options = new FolderFolioOptions { PhotoRoot = directory.Path, CacheRoot = directory.Path };
        var queue = new IndexRefreshQueue();
        var index = new PortfolioIndex();
        var clock = new RetryObservingTimeProvider();
        var coordinator = new IndexRefreshCoordinator(
            queue,
            new PhotoScanner(options, new ImageSharpMetadataReader()),
            index,
            clock);
        using var service = new IndexingService(
            options,
            queue,
            coordinator,
            index,
            new RecordingPhotoRootWatcher(),
            clock);

        await service.StartAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => index.Current.Status == IndexStatus.Ready, TestContext.Current.CancellationToken);
        var lastSuccessfulSnapshot = index.Current.Snapshot;

        Directory.Delete(directory.Path, recursive: true);
        queue.RequestFullScan();
        await clock.QuietPeriodScheduled.Task.WaitAsync(TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMilliseconds(750));
        await WaitUntilAsync(() => index.Current.Status == IndexStatus.Degraded, TestContext.Current.CancellationToken);
        var degradedGeneration = index.Current.Generation;
        await clock.RetryDelayScheduled.Task.WaitAsync(TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(5));
        await WaitUntilAsync(() => index.Current.Generation > degradedGeneration, TestContext.Current.CancellationToken);

        Assert.Equal(IndexStatus.Degraded, index.Current.Status);
        Assert.Same(lastSuccessfulSnapshot, index.Current.Snapshot);
        await service.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_recovered_root_starts_a_fresh_watcher_before_scanning_and_observes_later_changes()
    {
        using var directory = new TemporaryDirectory();
        var photoRoot = directory.CreateDirectory("photos");
        var options = new FolderFolioOptions { PhotoRoot = photoRoot, CacheRoot = directory.Path };
        var queue = new IndexRefreshQueue();
        var index = new PortfolioIndex();
        var clock = new RetryObservingTimeProvider();
        var scanner = new StubPhotoScanner();
        var watcher = new LifecyclePhotoRootWatcher(queue);
        var watcherStartsAtFullScans = new List<int>();
        scanner.OnScanAll = () => watcherStartsAtFullScans.Add(watcher.StartCount);
        using var service = new IndexingService(
            options,
            queue,
            new IndexRefreshCoordinator(queue, scanner, index, clock),
            index,
            watcher,
            clock);

        await service.StartAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => index.Current.Status == IndexStatus.Ready, TestContext.Current.CancellationToken);

        Directory.Delete(photoRoot, recursive: true);
        queue.RequestFullScan();
        await WaitUntilAsync(() => clock.QuietPeriodScheduleCount >= 1, TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMilliseconds(750));
        await WaitUntilAsync(() => index.Current.Status == IndexStatus.Degraded, TestContext.Current.CancellationToken);
        await clock.RetryDelayScheduled.Task.WaitAsync(TestContext.Current.CancellationToken);

        Directory.CreateDirectory(photoRoot);
        clock.Advance(TimeSpan.FromSeconds(5));
        await WaitUntilAsync(() => scanner.ScanAllCallCount >= 2, TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => clock.QuietPeriodScheduleCount >= 2, TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMilliseconds(750));
        await WaitUntilAsync(() => scanner.ScanAllCallCount >= 3, TestContext.Current.CancellationToken);

        var quietPeriodsBeforeChange = clock.QuietPeriodScheduleCount;
        watcher.EmitAlbumChange("01-Trip");
        await WaitUntilAsync(
            () => clock.QuietPeriodScheduleCount > quietPeriodsBeforeChange,
            TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMilliseconds(750));
        await WaitUntilAsync(() => scanner.RescanAlbumsCallCount == 1, TestContext.Current.CancellationToken);

        Assert.Equal([1, 2, 2], watcherStartsAtFullScans);
        Assert.Equal(2, watcher.StartCount);
        Assert.Equal(1, watcher.StopCount);
        Assert.Equal(["01-Trip"], scanner.LastAlbumDirectoryNames);
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

        public void Stop() => Started = false;

        public void Dispose() { }
    }

    private sealed class LifecyclePhotoRootWatcher(IIndexRefreshQueue queue) : IPhotoRootWatcher
    {
        private bool started;

        public int StartCount { get; private set; }

        public int StopCount { get; private set; }

        public void Start()
        {
            StartCount++;
            started = true;
        }

        public void Stop()
        {
            StopCount++;
            started = false;
        }

        public void EmitAlbumChange(string albumDirectoryName)
        {
            if (!started)
            {
                throw new InvalidOperationException("The watcher is not active.");
            }

            queue.RequestAlbum(albumDirectoryName);
        }

        public void Dispose() => started = false;
    }

    private sealed class RetryObservingTimeProvider : TimeProvider
    {
        private readonly FakeTimeProvider clock = new();
        private int quietPeriodScheduleCount;

        public TaskCompletionSource RetryDelayScheduled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource QuietPeriodScheduled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int QuietPeriodScheduleCount => Volatile.Read(ref quietPeriodScheduleCount);

        public override DateTimeOffset GetUtcNow() => clock.GetUtcNow();

        public override long GetTimestamp() => clock.GetTimestamp();

        public override long TimestampFrequency => clock.TimestampFrequency;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            if (dueTime == TimeSpan.FromSeconds(5))
            {
                RetryDelayScheduled.TrySetResult();
            }
            else if (dueTime == TimeSpan.FromMilliseconds(750))
            {
                Interlocked.Increment(ref quietPeriodScheduleCount);
                QuietPeriodScheduled.TrySetResult();
            }

            return clock.CreateTimer(callback, state, dueTime, period);
        }

        public void Advance(TimeSpan timeSpan) => clock.Advance(timeSpan);
    }
}

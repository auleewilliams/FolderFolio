namespace FolderFolio.Indexing;

public sealed class PhotoRootEventDebouncer : IDisposable
{
    private readonly object sync = new();
    private readonly IIndexRefreshQueue queue;
    private readonly TimeProvider clock;
    private readonly TimeSpan delay;
    private readonly ITimer timer;
    private readonly HashSet<string> pendingAlbums = new(StringComparer.Ordinal);
    private long lastEvent;
    private bool disposed;

    public PhotoRootEventDebouncer(IIndexRefreshQueue queue, TimeProvider clock, TimeSpan delay)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(delay, TimeSpan.Zero);
        this.queue = queue;
        this.clock = clock;
        this.delay = delay;
        timer = clock.CreateTimer(Flush, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public void Dispatch(IndexRefreshRequest? request)
    {
        lock (sync)
        {
            if (disposed || request is null)
            {
                return;
            }

            if (request.FullScan)
            {
                ResetCore();
                queue.RequestFullScan();
                return;
            }

            pendingAlbums.UnionWith(request.AlbumDirectoryNames);
            lastEvent = clock.GetTimestamp();
            timer.Change(delay, Timeout.InfiniteTimeSpan);
        }
    }

    public void Reset()
    {
        lock (sync)
        {
            if (!disposed)
            {
                ResetCore();
            }
        }
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed) return;
            disposed = true;
            pendingAlbums.Clear();
            timer.Dispose();
        }
    }

    private void ResetCore()
    {
        pendingAlbums.Clear();
        timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    private void Flush(object? state)
    {
        lock (sync)
        {
            if (disposed || pendingAlbums.Count == 0) return;
            var elapsed = clock.GetElapsedTime(lastEvent);
            if (elapsed < delay)
            {
                timer.Change(delay - elapsed, Timeout.InfiniteTimeSpan);
                return;
            }

            foreach (var album in pendingAlbums)
            {
                queue.RequestAlbum(album);
            }
            pendingAlbums.Clear();
        }
    }
}

using FolderFolio.Configuration;

namespace FolderFolio.Indexing;

public sealed class FileSystemPhotoRootWatcher : IPhotoRootWatcher
{
    private readonly object sync = new();
    private readonly string photoRoot;
    private readonly PhotoRootEventDebouncer debouncer;
    private readonly PhotoRootEventMapper mapper;
    private FileSystemWatcher? watcher;
    private bool disposed;

    public FileSystemPhotoRootWatcher(
        FolderFolioOptions options,
        IIndexRefreshQueue queue,
        PhotoRootEventMapper mapper,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.photoRoot = Path.GetFullPath(options.PhotoRoot);
        ArgumentNullException.ThrowIfNull(queue);
        debouncer = new PhotoRootEventDebouncer(queue, timeProvider ?? TimeProvider.System,
            TimeSpan.FromMilliseconds(options.WatcherDebounceMilliseconds));
        this.mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public void Start()
    {
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (watcher is not null)
            {
                return;
            }

            var fileSystemWatcher = new FileSystemWatcher(photoRoot)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.DirectoryName |
                    NotifyFilters.FileName |
                    NotifyFilters.Size |
                    NotifyFilters.LastWrite
            };
            fileSystemWatcher.Changed += OnPathChanged;
            fileSystemWatcher.Created += OnPathChanged;
            fileSystemWatcher.Deleted += OnPathChanged;
            fileSystemWatcher.Renamed += OnRenamed;
            fileSystemWatcher.Error += OnError;
            fileSystemWatcher.EnableRaisingEvents = true;
            watcher = fileSystemWatcher;
        }
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            StopCore();
            debouncer.Dispose();
        }
    }

    public void Stop()
    {
        lock (sync)
        {
            StopCore();
        }
    }

    private void StopCore()
    {
        watcher?.Dispose();
        watcher = null;
        debouncer.Reset();
    }

    private void OnPathChanged(object sender, FileSystemEventArgs eventArgs) => Dispatch(sender, mapper.MapPath(eventArgs.FullPath));

    private void OnRenamed(object sender, RenamedEventArgs eventArgs) => Dispatch(sender, mapper.MapRename(eventArgs.OldFullPath, eventArgs.FullPath));

    private void OnError(object sender, ErrorEventArgs eventArgs) => Dispatch(sender, IndexRefreshRequest.Full);

    private void Dispatch(object sender, IndexRefreshRequest? request)
    {
        lock (sync)
        {
            if (!disposed && ReferenceEquals(sender, watcher))
            {
                debouncer.Dispatch(request);
            }
        }
    }
}

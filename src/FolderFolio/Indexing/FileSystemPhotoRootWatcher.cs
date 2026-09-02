using FolderFolio.Configuration;

namespace FolderFolio.Indexing;

public sealed class FileSystemPhotoRootWatcher : IPhotoRootWatcher
{
    private readonly object sync = new();
    private readonly string photoRoot;
    private readonly IIndexRefreshQueue queue;
    private readonly PhotoRootEventMapper mapper;
    private FileSystemWatcher? watcher;
    private bool disposed;

    public FileSystemPhotoRootWatcher(
        FolderFolioOptions options,
        IIndexRefreshQueue queue,
        PhotoRootEventMapper mapper)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.photoRoot = Path.GetFullPath(options.PhotoRoot);
        this.queue = queue ?? throw new ArgumentNullException(nameof(queue));
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
            watcher?.Dispose();
            watcher = null;
        }
    }

    private void OnPathChanged(object sender, FileSystemEventArgs eventArgs) => Dispatch(mapper.MapPath(eventArgs.FullPath));

    private void OnRenamed(object sender, RenamedEventArgs eventArgs) => Dispatch(mapper.MapRename(eventArgs.OldFullPath, eventArgs.FullPath));

    private void OnError(object sender, ErrorEventArgs eventArgs) => queue.RequestFullScan();

    private void Dispatch(IndexRefreshRequest? request)
    {
        if (request is null)
        {
            return;
        }

        if (request.FullScan)
        {
            queue.RequestFullScan();
            return;
        }

        foreach (var albumDirectoryName in request.AlbumDirectoryNames)
        {
            queue.RequestAlbum(albumDirectoryName);
        }
    }
}

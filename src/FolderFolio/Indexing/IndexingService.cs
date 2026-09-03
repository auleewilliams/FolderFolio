using FolderFolio.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FolderFolio.Indexing;

public sealed class IndexingService : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    private readonly string photoRoot;
    private readonly IIndexRefreshQueue queue;
    private readonly IIndexRefreshCoordinatorFacade coordinator;
    private readonly IPortfolioIndex index;
    private readonly IPhotoRootWatcher watcher;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<IndexingService>? logger;

    public IndexingService(
        FolderFolioOptions options,
        IIndexRefreshQueue queue,
        IndexRefreshCoordinator coordinator,
        IPortfolioIndex index,
        IPhotoRootWatcher watcher,
        TimeProvider timeProvider,
        ILogger<IndexingService>? logger = null)
        : this(options, queue, new IndexRefreshCoordinatorFacade(coordinator), index, watcher, timeProvider, logger)
    {
    }

    internal IndexingService(
        FolderFolioOptions options,
        IIndexRefreshQueue queue,
        IIndexRefreshCoordinatorFacade coordinator,
        IPortfolioIndex index,
        IPhotoRootWatcher watcher,
        TimeProvider timeProvider,
        ILogger<IndexingService>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        photoRoot = Path.GetFullPath(options.PhotoRoot);
        this.queue = queue ?? throw new ArgumentNullException(nameof(queue));
        this.coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        this.index = index ?? throw new ArgumentNullException(nameof(index));
        this.watcher = watcher ?? throw new ArgumentNullException(nameof(watcher));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var watcherStarted = false;
        var hasPublishedSuccessfully = false;

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!Directory.Exists(photoRoot))
            {
                StopWatcher(ref watcherStarted);
                if (hasPublishedSuccessfully)
                {
                    index.MarkDegraded("Photo root is unavailable.");
                }
                else
                {
                    index.MarkStarting("Photo root is unavailable.");
                }

                await Task.Delay(RetryDelay, timeProvider, stoppingToken).ConfigureAwait(false);
                continue;
            }

            try
            {
                if (!watcherStarted)
                {
                    watcher.Start();
                    watcherStarted = true;
                }

                await coordinator.RefreshFullAsync(hasPublishedSuccessfully, stoppingToken).ConfigureAwait(false);
                hasPublishedSuccessfully = true;

                while (!stoppingToken.IsCancellationRequested)
                {
                    await coordinator.ProcessNextBatchAsync(IsPhotoRootAvailable, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                if (exception is DirectoryNotFoundException || !Directory.Exists(photoRoot))
                {
                    StopWatcher(ref watcherStarted);
                }

                logger?.LogError(exception, "Background photo indexing failed.");
                if (hasPublishedSuccessfully)
                {
                    index.MarkDegraded("Photo index refresh failed.");
                }
                else
                {
                    index.MarkStarting("Photo index is unavailable.");
                }

                queue.RequestFullScan();
                await Task.Delay(RetryDelay, timeProvider, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private bool IsPhotoRootAvailable() => Directory.Exists(photoRoot);

    private void StopWatcher(ref bool watcherStarted)
    {
        if (!watcherStarted)
        {
            return;
        }

        watcher.Stop();
        watcherStarted = false;
    }
}

internal interface IIndexRefreshCoordinatorFacade
{
    Task ProcessNextBatchAsync(Func<bool> isPhotoRootAvailable, CancellationToken cancellationToken);

    Task RefreshFullAsync(bool markFailureAsDegraded, CancellationToken cancellationToken);
}

internal sealed class IndexRefreshCoordinatorFacade(IndexRefreshCoordinator coordinator) : IIndexRefreshCoordinatorFacade
{
    public Task ProcessNextBatchAsync(Func<bool> isPhotoRootAvailable, CancellationToken cancellationToken) =>
        coordinator.ProcessNextBatchAsync(isPhotoRootAvailable, cancellationToken);

    public Task RefreshFullAsync(bool markFailureAsDegraded, CancellationToken cancellationToken) =>
        coordinator.RefreshFullAsync(markFailureAsDegraded, cancellationToken);
}

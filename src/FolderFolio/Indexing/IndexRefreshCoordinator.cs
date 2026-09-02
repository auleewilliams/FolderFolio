using System.Collections.Immutable;
using FolderFolio.Domain;
using Microsoft.Extensions.Logging;

namespace FolderFolio.Indexing;

public sealed class IndexRefreshCoordinator
{
    private static readonly TimeSpan QuietPeriod = TimeSpan.FromMilliseconds(750);

    private readonly IIndexRefreshQueue queue;
    private readonly IPhotoScanner scanner;
    private readonly IPortfolioIndex index;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<IndexRefreshCoordinator>? logger;

    public IndexRefreshCoordinator(
        IIndexRefreshQueue queue,
        IPhotoScanner scanner,
        IPortfolioIndex index,
        TimeProvider timeProvider,
        ILogger<IndexRefreshCoordinator>? logger = null)
    {
        this.queue = queue ?? throw new ArgumentNullException(nameof(queue));
        this.scanner = scanner ?? throw new ArgumentNullException(nameof(scanner));
        this.index = index ?? throw new ArgumentNullException(nameof(index));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.logger = logger;
    }

    public async Task ProcessNextBatchAsync(CancellationToken cancellationToken)
    {
        if (!await queue.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var request = DrainRequests();
        while (true)
        {
            using var quietPeriodCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var nextRequest = queue.WaitToReadAsync(quietPeriodCancellation.Token).AsTask();
            var quietPeriod = Task.Delay(QuietPeriod, timeProvider, cancellationToken);
            if (await Task.WhenAny(nextRequest, quietPeriod).ConfigureAwait(false) == quietPeriod)
            {
                await quietPeriod.ConfigureAwait(false);
                quietPeriodCancellation.Cancel();
                try
                {
                    await nextRequest.ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                }

                break;
            }

            if (!await nextRequest.ConfigureAwait(false))
            {
                break;
            }

            request = Combine(request, DrainRequests());
        }

        request = Combine(request, ConsumeForcedFullScan());
        await RefreshAsync(request, markFailureAsDegraded: true, cancellationToken).ConfigureAwait(false);
    }

    public Task RefreshFullAsync(bool markFailureAsDegraded, CancellationToken cancellationToken) =>
        RefreshAsync(IndexRefreshRequest.Full, markFailureAsDegraded, cancellationToken);

    private IndexRefreshRequest DrainRequests()
    {
        var combined = ConsumeForcedFullScan();
        while (queue.TryRead(out var request))
        {
            combined = Combine(combined, request);
        }

        return combined;
    }

    private IndexRefreshRequest ConsumeForcedFullScan() =>
        queue.ConsumeForcedFullScan() ? IndexRefreshRequest.Full : new IndexRefreshRequest(
            false,
            System.Collections.Immutable.ImmutableHashSet<string>.Empty.WithComparer(StringComparer.Ordinal));

    private async Task RefreshAsync(
        IndexRefreshRequest request,
        bool markFailureAsDegraded,
        CancellationToken cancellationToken)
    {
        var started = timeProvider.GetTimestamp();
        try
        {
            var result = request.FullScan
                ? await scanner.ScanAllAsync(cancellationToken).ConfigureAwait(false)
                : await scanner.RescanAlbumsAsync(index.Current.Snapshot, request.AlbumDirectoryNames, cancellationToken).ConfigureAwait(false);
            var duration = timeProvider.GetElapsedTime(started);
            index.PublishReady(result.Snapshot, timeProvider.GetUtcNow(), duration);
            logger?.LogInformation(
                "Completed {ScanKind} photo index refresh: {AlbumCount} albums, {PhotoCount} photos, {SkippedFileCount} skipped files in {ElapsedMilliseconds} ms.",
                request.FullScan ? "full" : "targeted",
                result.Snapshot.AlbumCount,
                result.Snapshot.PhotoCount,
                result.SkippedFileCount,
                duration.TotalMilliseconds);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger?.LogError(exception, "Photo index refresh failed.");
            if (markFailureAsDegraded)
            {
                index.MarkDegraded("Photo index refresh failed.");
            }

            throw;
        }
    }

    private static IndexRefreshRequest Combine(IndexRefreshRequest first, IndexRefreshRequest second)
    {
        if (first.FullScan || second.FullScan)
        {
            return IndexRefreshRequest.Full;
        }

        return new IndexRefreshRequest(
            false,
            first.AlbumDirectoryNames.Union(second.AlbumDirectoryNames, StringComparer.Ordinal).ToImmutableHashSet(StringComparer.Ordinal));
    }
}

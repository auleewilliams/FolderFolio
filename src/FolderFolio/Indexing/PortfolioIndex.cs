using System.Threading;
using FolderFolio.Domain;

namespace FolderFolio.Indexing;

public sealed class PortfolioIndex : IPortfolioIndex
{
    private readonly object _publicationLock = new();
    private long _generation;
    private IndexPublication _current = new(
        0,
        IndexStatus.Starting,
        PortfolioSnapshot.Empty,
        null,
        null,
        null);

    public IndexPublication Current => Volatile.Read(ref _current);

    public void PublishReady(PortfolioSnapshot snapshot, DateTimeOffset completedAtUtc, TimeSpan duration)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (_publicationLock)
        {
            Publish(new IndexPublication(
                NextGeneration(),
                IndexStatus.Ready,
                snapshot,
                completedAtUtc,
                duration,
                null));
        }
    }

    public void MarkStarting(string? publicError = null)
    {
        lock (_publicationLock)
        {
            var current = Current;
            Publish(new IndexPublication(
                NextGeneration(),
                IndexStatus.Starting,
                current.Snapshot,
                current.LastSuccessAtUtc,
                current.LastSuccessDuration,
                SanitizeError(publicError)));
        }
    }

    public void MarkDegraded(string publicError)
    {
        lock (_publicationLock)
        {
            var current = Current;
            Publish(new IndexPublication(
                NextGeneration(),
                IndexStatus.Degraded,
                current.Snapshot,
                current.LastSuccessAtUtc,
                current.LastSuccessDuration,
                SanitizeError(publicError)));
        }
    }

    private long NextGeneration() => Interlocked.Increment(ref _generation);

    private void Publish(IndexPublication publication) => Interlocked.Exchange(ref _current, publication);

    private static string? SanitizeError(string? publicError)
    {
        if (string.IsNullOrWhiteSpace(publicError))
        {
            return null;
        }

        return string.Join(' ', publicError.Split(["\r\n", "\n", "\r"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}

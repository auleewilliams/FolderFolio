using System.Collections.Concurrent;
using System.Collections.Immutable;
using FolderFolio.Domain;
using FolderFolio.Indexing;
using Xunit;

namespace FolderFolio.Tests.Indexing;

public sealed class PortfolioIndexTests
{
    [Fact]
    public async Task Readers_observe_only_complete_old_or_new_publications_during_concurrent_updates()
    {
        var oldSnapshot = Snapshot("Old", 1);
        var newSnapshot = Snapshot("New", 2);
        var index = new PortfolioIndex();
        index.PublishReady(oldSnapshot, DateTimeOffset.UnixEpoch, TimeSpan.FromSeconds(1));
        var observed = new ConcurrentQueue<(string Title, int PhotoCount)>();
        var cancellationToken = TestContext.Current.CancellationToken;
        using var start = new Barrier(5);

        var writer = Task.Run(() =>
        {
            start.SignalAndWait(cancellationToken);
            for (var iteration = 0; iteration < 100_000; iteration++)
            {
                index.PublishReady(iteration % 2 == 0 ? newSnapshot : oldSnapshot, DateTimeOffset.UnixEpoch, TimeSpan.FromSeconds(1));
            }
        }, cancellationToken);
        var readers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            start.SignalAndWait(cancellationToken);
            while (!writer.IsCompleted)
            {
                var snapshot = index.Current.Snapshot;
                observed.Enqueue((snapshot.Albums[0].Title, snapshot.PhotoCount));
            }
        }, cancellationToken));

        await Task.WhenAll(readers.Append(writer));

        Assert.NotEmpty(observed);
        Assert.All(observed, value => Assert.True(value is ("Old", 1) or ("New", 2)));
    }

    [Fact]
    public void Marking_degraded_preserves_the_last_ready_snapshot()
    {
        var snapshot = Snapshot("Ready", 1);
        var index = new PortfolioIndex();
        index.PublishReady(snapshot, DateTimeOffset.UnixEpoch, TimeSpan.FromSeconds(1));

        index.MarkDegraded("Scan failed");

        var publication = index.Current;
        Assert.Equal(IndexStatus.Degraded, publication.Status);
        Assert.Same(snapshot, publication.Snapshot);
        Assert.Equal("Scan failed", publication.PublicError);
    }

    private static PortfolioSnapshot Snapshot(string title, int photoCount)
    {
        var photos = Enumerable.Range(0, photoCount)
            .Select(index => new IndexedPhoto(
                index.ToString(),
                $"{index}.jpg",
                new SourceFingerprint($"Album/{index}.jpg", 1, 1),
                null,
                1,
                1))
            .ToImmutableArray();
        var album = new IndexedAlbum("Album", "album", title, "album", null, photos);
        return new PortfolioSnapshot(ImmutableArray.Create(album));
    }
}

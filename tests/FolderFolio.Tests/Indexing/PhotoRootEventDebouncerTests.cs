using FolderFolio.Indexing;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace FolderFolio.Tests.Indexing;

public sealed class PhotoRootEventDebouncerTests
{
    [Fact]
    public void Fifty_photo_changes_emit_one_album_request_after_the_last_event_quiet_period()
    {
        var queue = new IndexRefreshQueue();
        var clock = new FakeTimeProvider();
        using var debouncer = new PhotoRootEventDebouncer(queue, clock, TimeSpan.FromSeconds(2));
        for (var photo = 0; photo < 50; photo++)
        {
            debouncer.Dispatch(IndexRefreshRequest.Album("01-Trip"));
            clock.Advance(TimeSpan.FromMilliseconds(10));
        }
        clock.Advance(TimeSpan.FromMilliseconds(1989));
        Assert.False(queue.TryRead(out _));

        clock.Advance(TimeSpan.FromMilliseconds(1));

        Assert.True(queue.TryRead(out var request));
        Assert.Equal(["01-Trip"], request.AlbumDirectoryNames);
        Assert.False(queue.TryRead(out _));
    }

    [Fact]
    public void Distinct_albums_are_retained_and_full_scans_bypass_the_window_and_supersede_pending_changes()
    {
        var queue = new IndexRefreshQueue();
        var clock = new FakeTimeProvider();
        using var debouncer = new PhotoRootEventDebouncer(queue, clock, TimeSpan.FromSeconds(3));
        debouncer.Dispatch(IndexRefreshRequest.Album("one"));
        debouncer.Dispatch(IndexRefreshRequest.Album("two"));
        clock.Advance(TimeSpan.FromSeconds(3));
        Assert.True(queue.TryRead(out var first));
        Assert.True(queue.TryRead(out var second));
        Assert.Equal(["one", "two"], first.AlbumDirectoryNames.Concat(second.AlbumDirectoryNames).Order());

        debouncer.Dispatch(IndexRefreshRequest.Album("pending"));
        debouncer.Dispatch(IndexRefreshRequest.Full);

        Assert.True(queue.TryRead(out var full));
        Assert.True(full.FullScan);
        clock.Advance(TimeSpan.FromSeconds(3));
        Assert.False(queue.TryRead(out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Stopping_or_disposing_discards_pending_events(bool dispose)
    {
        var queue = new IndexRefreshQueue();
        var clock = new FakeTimeProvider();
        using var debouncer = new PhotoRootEventDebouncer(queue, clock, TimeSpan.FromSeconds(2));
        debouncer.Dispatch(IndexRefreshRequest.Album("pending"));
        if (dispose) debouncer.Dispose();
        else debouncer.Reset();
        clock.Advance(TimeSpan.FromSeconds(2));
        Assert.False(queue.TryRead(out _));
    }
}

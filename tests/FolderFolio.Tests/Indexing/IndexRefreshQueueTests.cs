using FolderFolio.Indexing;
using Xunit;

namespace FolderFolio.Tests.Indexing;

public sealed class IndexRefreshQueueTests
{
    [Fact]
    public void A_full_queue_escalates_an_album_request_to_a_full_scan()
    {
        var queue = new IndexRefreshQueue(capacity: 2);

        queue.RequestAlbum("01-Landscapes");
        queue.RequestAlbum("02-Portraits");
        queue.RequestAlbum("03-Travel");

        Assert.True(queue.ConsumeForcedFullScan());
        Assert.False(queue.ConsumeForcedFullScan());
    }

    [Fact]
    public async Task A_full_request_wakes_a_waiting_reader()
    {
        var queue = new IndexRefreshQueue(capacity: 2);

        queue.RequestFullScan();

        Assert.True(await queue.WaitToReadAsync(TestContext.Current.CancellationToken));
        Assert.True(queue.ConsumeForcedFullScan());
    }
}

using System.Threading.Channels;

namespace FolderFolio.Indexing;

public sealed class IndexRefreshQueue : IIndexRefreshQueue
{
    private readonly Channel<IndexRefreshRequest> channel;
    private int forcedFullScan;

    public IndexRefreshQueue(int capacity = 128)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        channel = Channel.CreateBounded<IndexRefreshRequest>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    }

    public void RequestFullScan()
    {
        Interlocked.Exchange(ref forcedFullScan, 1);
        channel.Writer.TryWrite(IndexRefreshRequest.Full);
    }

    public void RequestAlbum(string albumDirectoryName)
    {
        var request = IndexRefreshRequest.Album(albumDirectoryName);
        if (!channel.Writer.TryWrite(request))
        {
            Interlocked.Exchange(ref forcedFullScan, 1);
            channel.Writer.TryWrite(IndexRefreshRequest.Full);
        }
    }

    public ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken) =>
        channel.Reader.WaitToReadAsync(cancellationToken);

    public bool TryRead(out IndexRefreshRequest request) => channel.Reader.TryRead(out request!);

    public bool ConsumeForcedFullScan() => Interlocked.Exchange(ref forcedFullScan, 0) == 1;
}

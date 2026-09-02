namespace FolderFolio.Indexing;

public interface IIndexRefreshQueue
{
    void RequestFullScan();

    void RequestAlbum(string albumDirectoryName);

    ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken);

    bool TryRead(out IndexRefreshRequest request);

    bool ConsumeForcedFullScan();
}

using FolderFolio.Domain;
using FolderFolio.Indexing;

namespace FolderFolio.Tests.Support;

public sealed class StubPhotoScanner : IPhotoScanner
{
    public PhotoScanResult AllResult { get; set; } = new(PortfolioSnapshot.Empty, 0);

    public PhotoScanResult AlbumsResult { get; set; } = new(PortfolioSnapshot.Empty, 0);

    public Exception? Exception { get; set; }

    public Action? OnScanAll { get; set; }

    public int ScanAllCallCount { get; private set; }

    public int RescanAlbumsCallCount { get; private set; }

    public IReadOnlySet<string>? LastAlbumDirectoryNames { get; private set; }

    public Task<PhotoScanResult> ScanAllAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ScanAllCallCount++;
        OnScanAll?.Invoke();
        return Exception is null ? Task.FromResult(AllResult) : Task.FromException<PhotoScanResult>(Exception);
    }

    public Task<PhotoScanResult> RescanAlbumsAsync(
        PortfolioSnapshot current,
        IReadOnlySet<string> albumDirectoryNames,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RescanAlbumsCallCount++;
        LastAlbumDirectoryNames = new HashSet<string>(albumDirectoryNames, StringComparer.Ordinal);
        return Exception is null ? Task.FromResult(AlbumsResult) : Task.FromException<PhotoScanResult>(Exception);
    }
}

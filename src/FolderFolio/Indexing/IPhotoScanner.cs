using FolderFolio.Domain;

namespace FolderFolio.Indexing;

public interface IPhotoScanner
{
    Task<PhotoScanResult> ScanAllAsync(CancellationToken cancellationToken);

    Task<PhotoScanResult> RescanAlbumsAsync(
        PortfolioSnapshot current,
        IReadOnlySet<string> albumDirectoryNames,
        CancellationToken cancellationToken);
}

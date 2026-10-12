using FolderFolio.Domain;

namespace FolderFolio.Imaging;

public interface IImageDerivativeGenerator
{
    Task WriteAvifAsync(IndexedPhoto photo, Stream destination, int maxLongEdge, int quality, CancellationToken cancellationToken);

    Task WriteWebPAsync(
        IndexedPhoto photo,
        Stream destination,
        int maxLongEdge,
        int quality,
        CancellationToken cancellationToken);
}

using FolderFolio.Domain;

namespace FolderFolio.Imaging;

public interface IImageDerivativeGenerator
{
    Task WriteWebPAsync(
        IndexedPhoto photo,
        Stream destination,
        int maxLongEdge,
        int quality,
        CancellationToken cancellationToken);
}

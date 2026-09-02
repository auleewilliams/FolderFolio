namespace FolderFolio.Imaging;

public interface IImageDerivativeGenerator
{
    Task WriteWebPAsync(
        string sourcePath,
        Stream destination,
        int maxLongEdge,
        int quality,
        CancellationToken cancellationToken);
}

namespace FolderFolio.Indexing;

public interface IImageMetadataReader
{
    Task<PhotoSourceMetadata> IdentifyAsync(
        string sourcePath,
        CancellationToken cancellationToken);
}

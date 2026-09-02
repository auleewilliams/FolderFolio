namespace FolderFolio.Imaging;

public sealed record CachedDerivative(
    string AbsolutePath,
    long Length,
    DateTimeOffset LastModifiedUtc);

namespace FolderFolio.Imaging;

public sealed record DerivativeIdentity(
    string CacheKey,
    string Version,
    string ETag,
    int MaxLongEdge,
    int WebPQuality);

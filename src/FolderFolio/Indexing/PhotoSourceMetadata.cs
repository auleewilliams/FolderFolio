namespace FolderFolio.Indexing;

public sealed record PhotoSourceMetadata(
    int Width,
    int Height,
    DateTime? CapturedAt,
    long EstimatedPixelBytes);

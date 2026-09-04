namespace FolderFolio.Domain;

public sealed record IndexedPhoto(
    string Id,
    string FileName,
    SourceFingerprint Source,
    DateTime? CapturedAt,
    int Width,
    int Height);

namespace FolderFolio.Domain;

public sealed record SourceFingerprint(string RelativePath, long Length, long LastWriteUtcTicks);

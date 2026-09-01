using System.Collections.Immutable;

namespace FolderFolio.Domain;

public sealed record ScannedAlbum(
    string DirectoryName,
    string Title,
    string BaseSlug,
    int? SortPrefix,
    ImmutableArray<IndexedPhoto> Photos);

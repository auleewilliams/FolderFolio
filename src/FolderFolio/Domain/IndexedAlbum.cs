using System.Collections.Immutable;

namespace FolderFolio.Domain;

public sealed record IndexedAlbum(
    string DirectoryName,
    string Slug,
    string Title,
    string BaseSlug,
    int? SortPrefix,
    ImmutableArray<IndexedPhoto> Photos)
{
    public IndexedPhoto? Cover => Photos.IsDefaultOrEmpty ? null : Photos[0];
}

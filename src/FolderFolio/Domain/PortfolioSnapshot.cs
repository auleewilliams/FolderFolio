using System.Collections.Immutable;

namespace FolderFolio.Domain;

public sealed record PortfolioSnapshot
{
    public PortfolioSnapshot(ImmutableArray<IndexedAlbum> albums)
        : this(albums, BuildAlbumsBySlug(albums))
    {
    }

    public PortfolioSnapshot(
        ImmutableArray<IndexedAlbum> albums,
        ImmutableDictionary<string, IndexedAlbum> albumsBySlug)
    {
        Albums = albums.IsDefault ? ImmutableArray<IndexedAlbum>.Empty : albums;
        AlbumsBySlug = albumsBySlug.WithComparers(StringComparer.OrdinalIgnoreCase);
    }

    public ImmutableArray<IndexedAlbum> Albums { get; }

    public ImmutableDictionary<string, IndexedAlbum> AlbumsBySlug { get; }

    public static PortfolioSnapshot Empty { get; } = new(ImmutableArray<IndexedAlbum>.Empty);

    public int AlbumCount => Albums.Length;

    public int PhotoCount => Albums.Sum(album => album.Photos.Length);

    private static ImmutableDictionary<string, IndexedAlbum> BuildAlbumsBySlug(ImmutableArray<IndexedAlbum> albums) =>
        (albums.IsDefault ? ImmutableArray<IndexedAlbum>.Empty : albums)
        .ToImmutableDictionary(album => album.Slug, StringComparer.OrdinalIgnoreCase);
}

using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using FolderFolio.Domain;

namespace FolderFolio.Indexing;

public static class AlbumCatalogBuilder
{
    public static ImmutableArray<IndexedAlbum> Build(IEnumerable<ScannedAlbum> scannedAlbums)
    {
        ArgumentNullException.ThrowIfNull(scannedAlbums);

        var groups = scannedAlbums.GroupBy(album => album.BaseSlug, StringComparer.OrdinalIgnoreCase);
        var albums = new List<IndexedAlbum>();

        foreach (var group in groups)
        {
            var hasCollision = group.Skip(1).Any();
            foreach (var scannedAlbum in group)
            {
                var slug = hasCollision
                    ? $"{scannedAlbum.BaseSlug}--{DirectoryNameHash(scannedAlbum.DirectoryName)}"
                    : scannedAlbum.BaseSlug;
                albums.Add(new IndexedAlbum(
                    scannedAlbum.DirectoryName,
                    slug,
                    scannedAlbum.Title,
                    scannedAlbum.BaseSlug,
                    scannedAlbum.SortPrefix,
                    scannedAlbum.Photos.IsDefault ? ImmutableArray<IndexedPhoto>.Empty : scannedAlbum.Photos));
            }
        }

        return albums
            .OrderBy(album => album.SortPrefix.HasValue ? 0 : 1)
            .ThenBy(album => album.SortPrefix ?? int.MaxValue)
            .ThenBy(album => album.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(album => album.Title, StringComparer.Ordinal)
            .ThenBy(album => album.DirectoryName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(album => album.DirectoryName, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    private static string DirectoryNameHash(string directoryName) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(directoryName))).ToLowerInvariant()[..8];
}

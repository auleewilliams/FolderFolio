using System.Collections.Immutable;
using FolderFolio.Domain;
using FolderFolio.Indexing;
using Xunit;

namespace FolderFolio.Tests.Indexing;

public sealed class AlbumCatalogBuilderTests
{
    [Fact]
    public void Colliding_album_slugs_are_stable_when_input_order_is_reversed()
    {
        var first = Album("01-Summer");
        var second = Album("02_Summer");

        var forward = AlbumCatalogBuilder.Build([first, second]);
        var reversed = AlbumCatalogBuilder.Build([second, first]);

        Assert.All(forward, album => Assert.StartsWith("summer--", album.Slug));
        Assert.NotEqual(forward[0].Slug, forward[1].Slug);
        Assert.Equal(
            forward.ToDictionary(album => album.DirectoryName, album => album.Slug),
            reversed.ToDictionary(album => album.DirectoryName, album => album.Slug));
    }

    [Fact]
    public void Prefixed_albums_sort_before_unprefixed_albums_by_numeric_prefix()
    {
        var catalog = AlbumCatalogBuilder.Build([Album("Portraits"), Album("10-Ten"), Album("02-Two")]);

        Assert.Equal(["02-Two", "10-Ten", "Portraits"], catalog.Select(album => album.DirectoryName));
    }

    private static ScannedAlbum Album(string directoryName)
    {
        var name = AlbumNameParser.Parse(directoryName);
        return new ScannedAlbum(directoryName, name.Title, name.BaseSlug, name.SortPrefix, ImmutableArray<IndexedPhoto>.Empty);
    }
}

using FolderFolio.Indexing;
using Xunit;

namespace FolderFolio.Tests.Indexing;

public sealed class PhotoRootEventMapperTests
{
    private readonly PhotoRootEventMapper mapper = new("/photos");

    [Fact]
    public void Maps_a_direct_photo_change_to_its_album_directory_name()
    {
        var request = mapper.MapPath("/photos/01-Landscapes/a.jpg");

        Assert.NotNull(request);
        Assert.False(request.FullScan);
        Assert.Equal(["01-Landscapes"], request.AlbumDirectoryNames);
    }

    [Fact]
    public void Ignores_photos_in_nested_directories_and_paths_outside_the_root()
    {
        Assert.Null(mapper.MapPath("/photos/01-Landscapes/nested/a.jpg"));
        Assert.Null(mapper.MapPath("/photos-archive/01-Landscapes/a.jpg"));
    }

    [Fact]
    public void Maps_top_level_album_changes_to_a_full_scan()
    {
        var request = mapper.MapPath("/photos/01-Landscapes");

        Assert.NotNull(request);
        Assert.True(request.FullScan);
    }

    [Fact]
    public void Maps_cross_album_renames_to_both_album_directory_names()
    {
        var request = mapper.MapRename("/photos/01-Landscapes/a.jpg", "/photos/02-Portraits/a.jpg");

        Assert.NotNull(request);
        Assert.False(request.FullScan);
        Assert.Equal(["01-Landscapes", "02-Portraits"], request.AlbumDirectoryNames.Order());
    }
}

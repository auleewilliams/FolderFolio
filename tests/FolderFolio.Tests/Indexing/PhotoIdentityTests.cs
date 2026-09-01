using FolderFolio.Indexing;
using Xunit;

namespace FolderFolio.Tests.Indexing;

public sealed class PhotoIdentityTests
{
    [Fact]
    public void Normalizes_path_separators_but_changes_identity_when_photo_is_renamed()
    {
        var backslashPath = PhotoIdentity.FromRelativePath("Album\\Photo.jpg");
        var slashPath = PhotoIdentity.FromRelativePath("Album/Photo.jpg");
        var renamedPath = PhotoIdentity.FromRelativePath("Album/Renamed.jpg");

        Assert.Equal(backslashPath, slashPath);
        Assert.NotEqual(slashPath, renamedPath);
    }
}

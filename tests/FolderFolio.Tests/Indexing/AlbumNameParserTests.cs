using FolderFolio.Indexing;
using Xunit;

namespace FolderFolio.Tests.Indexing;

public sealed class AlbumNameParserTests
{
    [Theory]
    [InlineData("01-Landscapes_and-Sea", 1, "Landscapes and Sea", "landscapes-and-sea")]
    [InlineData("Portraits", null, "Portraits", "portraits")]
    [InlineData("猫", null, "猫", "album")]
    public void Parses_album_directory_names(string input, int? order, string title, string slug)
    {
        var result = AlbumNameParser.Parse(input);

        Assert.Equal(order, result.SortPrefix);
        Assert.Equal(title, result.Title);
        Assert.Equal(slug, result.BaseSlug);
    }
}

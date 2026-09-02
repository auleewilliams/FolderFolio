using FolderFolio.Imaging;
using FolderFolio.Tests.Support;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using Xunit;

namespace FolderFolio.Tests.Imaging;

public sealed class ImageSharpDerivativeGeneratorTests
{
    [Fact]
    public async Task WriteWebPAsync_orients_resizes_and_removes_private_metadata()
    {
        using var directory = new TemporaryDirectory();
        var sourcePath = directory.FilePath("rotated.jpg");
        ImageFixtureFactory.CreateJpeg(sourcePath, width: 40, height: 80, orientation: 6, includePrivateMetadata: true);
        using (var source = await Image.LoadAsync(sourcePath, TestContext.Current.CancellationToken))
        {
            Assert.NotNull(source.Metadata.ExifProfile);
            Assert.NotNull(source.Metadata.IptcProfile);
            Assert.NotNull(source.Metadata.XmpProfile);
        }

        await using var destination = new MemoryStream();
        var generator = new ImageSharpDerivativeGenerator();

        await generator.WriteWebPAsync(sourcePath, destination, maxLongEdge: 50, quality: 80, TestContext.Current.CancellationToken);

        destination.Position = 0;
        using var derivative = await Image.LoadAsync(destination, TestContext.Current.CancellationToken);
        Assert.Equal(50, derivative.Width);
        Assert.Equal(25, derivative.Height);
        Assert.IsType<WebpFormat>(derivative.Metadata.DecodedImageFormat);
        Assert.Null(derivative.Metadata.ExifProfile);
        Assert.Null(derivative.Metadata.IptcProfile);
        Assert.Null(derivative.Metadata.XmpProfile);
    }

    [Fact]
    public async Task WriteWebPAsync_does_not_enlarge_smaller_sources()
    {
        using var directory = new TemporaryDirectory();
        var sourcePath = directory.FilePath("small.jpg");
        ImageFixtureFactory.CreateJpeg(sourcePath, width: 20, height: 10);
        await using var destination = new MemoryStream();
        var generator = new ImageSharpDerivativeGenerator();

        await generator.WriteWebPAsync(sourcePath, destination, maxLongEdge: 400, quality: 80, TestContext.Current.CancellationToken);

        destination.Position = 0;
        using var derivative = await Image.LoadAsync(destination, TestContext.Current.CancellationToken);
        Assert.Equal(20, derivative.Width);
        Assert.Equal(10, derivative.Height);
    }
}

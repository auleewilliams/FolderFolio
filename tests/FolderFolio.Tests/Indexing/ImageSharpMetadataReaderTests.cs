using FolderFolio.Indexing;
using FolderFolio.Tests.Support;
using Xunit;

namespace FolderFolio.Tests.Indexing;

public sealed class ImageSharpMetadataReaderTests
{
    [Fact]
    public async Task IdentifyAsync_reads_dimensions_original_capture_date_and_pixel_estimate()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.FilePath("original.jpg");
        ImageFixtureFactory.CreateJpeg(path);
        var reader = new ImageSharpMetadataReader();

        var metadata = await reader.IdentifyAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(120, metadata.Width);
        Assert.Equal(80, metadata.Height);
        Assert.Equal(new DateTime(2024, 3, 2, 10, 11, 12), metadata.CapturedAt);
        Assert.True(metadata.EstimatedPixelBytes > 0);
    }

    [Fact]
    public async Task IdentifyAsync_uses_digitized_capture_date_when_original_is_missing()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.FilePath("digitized.jpg");
        ImageFixtureFactory.CreateJpeg(path, dateTimeOriginal: null, dateTimeDigitized: "2024:04:03 01:02:03");
        var reader = new ImageSharpMetadataReader();

        var metadata = await reader.IdentifyAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(new DateTime(2024, 4, 3, 1, 2, 3), metadata.CapturedAt);
    }

    [Fact]
    public async Task IdentifyAsync_returns_no_capture_date_for_malformed_exif_timestamp()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.FilePath("malformed.jpg");
        ImageFixtureFactory.CreateJpeg(path, dateTimeOriginal: "not an exif timestamp");
        var reader = new ImageSharpMetadataReader();

        var metadata = await reader.IdentifyAsync(path, TestContext.Current.CancellationToken);

        Assert.Null(metadata.CapturedAt);
    }

    [Theory]
    [InlineData((ushort)5)]
    [InlineData((ushort)6)]
    [InlineData((ushort)7)]
    [InlineData((ushort)8)]
    public async Task IdentifyAsync_swaps_logical_dimensions_for_transposed_orientations(ushort orientation)
    {
        using var directory = new TemporaryDirectory();
        var path = directory.FilePath("rotated.jpg");
        ImageFixtureFactory.CreateJpeg(path, orientation: orientation);
        var reader = new ImageSharpMetadataReader();

        var metadata = await reader.IdentifyAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(80, metadata.Width);
        Assert.Equal(120, metadata.Height);
    }
}

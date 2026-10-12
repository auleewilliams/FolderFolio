using FolderFolio.Configuration;
using FolderFolio.Domain;
using FolderFolio.Imaging;
using FolderFolio.Indexing;
using FolderFolio.Tests.Support;
using ImageMagick;
using SixLabors.ImageSharp.Formats.Webp;
using Image = SixLabors.ImageSharp.Image;
using Xunit;

namespace FolderFolio.Tests.Imaging;

public sealed class AvifSupportTests
{
    [Fact]
    public async Task Scanner_indexes_real_AVIF_with_capture_date_and_skips_corrupt_sources()
    {
        using var root = new TemporaryDirectory();
        var album = root.CreateDirectory("01-Album");
        var jpeg = Path.Combine(album, "source.jpg");
        ImageFixtureFactory.CreateJpeg(jpeg);
        using (var image = new MagickImage(new FileInfo(jpeg)))
            image.Write(Path.Combine(album, "photo.AVIF"), MagickFormat.Avif);
        File.WriteAllText(Path.Combine(album, "corrupt.avif"), "not an image");

        var result = await new PhotoScanner(new FolderFolioOptions { PhotoRoot = root.Path }, new ImageSharpMetadataReader())
            .ScanAllAsync(TestContext.Current.CancellationToken);

        var photo = Assert.Single(result.Snapshot.Albums[0].Photos, photo => photo.FileName == "photo.AVIF");
        Assert.Equal(120, photo.Width);
        Assert.Equal(80, photo.Height);
        Assert.Equal(new DateTime(2024, 3, 2, 10, 11, 12), photo.CapturedAt);
        Assert.Equal(1, result.SkippedFileCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AVIF_derivatives_from_JPEG_and_AVIF_are_resized_oriented_and_remove_private_profiles(bool avifSource)
    {
        using var root = new TemporaryDirectory();
        var path = Path.Combine(root.Path, "source.jpg");
        ImageFixtureFactory.CreateJpeg(path, width: 40, height: 80, orientation: 6, includePrivateMetadata: true);
        if (avifSource)
        {
            using var input = new MagickImage(new FileInfo(path));
            path = Path.Combine(root.Path, "source.avif");
            input.Write(path, MagickFormat.Avif);
        }
        var file = new FileInfo(path);
        var photo = new IndexedPhoto("photo", file.Name, new SourceFingerprint(file.Name, file.Length, file.LastWriteTimeUtc.Ticks), null, 80, 40);
        var generator = new ImageSharpDerivativeGenerator(new SourcePathGuard(new FolderFolioOptions { PhotoRoot = root.Path }));
        await using var output = new MemoryStream();

        await generator.WriteAvifAsync(photo, output, 50, 65, TestContext.Current.CancellationToken);

        using var decoded = new MagickImage(output.ToArray());
        Assert.Equal(MagickFormat.Avif, decoded.Format);
        Assert.Equal(50u, decoded.Width);
        Assert.Equal(25u, decoded.Height);
        Assert.Null(decoded.GetExifProfile());
        Assert.Null(decoded.GetIptcProfile());
        Assert.Null(decoded.GetXmpProfile());

        if (avifSource)
        {
            await using var webp = new MemoryStream();
            await generator.WriteWebPAsync(photo, webp, 50, 82, TestContext.Current.CancellationToken);
            webp.Position = 0;
            using var webImage = await Image.LoadAsync(webp, TestContext.Current.CancellationToken);
            Assert.IsType<WebpFormat>(webImage.Metadata.DecodedImageFormat);
            Assert.Equal(50, webImage.Width);
            Assert.Equal(25, webImage.Height);
            Assert.Null(webImage.Metadata.ExifProfile);
        }
    }

    [Fact]
    public void AVIF_quality_and_format_change_only_AVIF_cache_identity()
    {
        var photo = new IndexedPhoto("photo", "photo.jpg", new SourceFingerprint("Album/photo.jpg", 3, 123), null, 120, 80);
        var factory = new DerivativeKeyFactory(new FolderFolioOptions());
        var changed = new DerivativeKeyFactory(new FolderFolioOptions { AvifQuality = 70 });

        Assert.Equal(factory.Create(photo, DerivativeKind.Web), changed.Create(photo, DerivativeKind.Web));
        Assert.Equal("2828c1cb335f6b2c4ec2a3644a1c8296d6a334637f39867e4287d4bf488ae0f0", factory.Create(photo, DerivativeKind.Web).CacheKey);
        Assert.NotEqual(factory.Create(photo, DerivativeKind.Avif).CacheKey, changed.Create(photo, DerivativeKind.Avif).CacheKey);
        Assert.NotEqual(factory.Create(photo, DerivativeKind.Avif).CacheKey, factory.Create(photo, DerivativeKind.Web).CacheKey);
        Assert.Equal("avif", factory.Create(photo, DerivativeKind.Avif).Format);
    }
}

using FolderFolio.Configuration;
using FolderFolio.Domain;
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
        var root = directory.CreateDirectory("photos");
        var sourcePath = Path.Combine(root, "rotated.jpg");
        ImageFixtureFactory.CreateJpeg(sourcePath, width: 40, height: 80, orientation: 6, includePrivateMetadata: true);
        using (var source = await Image.LoadAsync(sourcePath, TestContext.Current.CancellationToken))
        {
            Assert.NotNull(source.Metadata.ExifProfile);
            Assert.NotNull(source.Metadata.IptcProfile);
            Assert.NotNull(source.Metadata.XmpProfile);
        }

        await using var destination = new MemoryStream();
        var generator = Generator(root);

        await generator.WriteWebPAsync(Photo(root, sourcePath), destination, maxLongEdge: 50, quality: 80, TestContext.Current.CancellationToken);

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
        var root = directory.CreateDirectory("photos");
        var sourcePath = Path.Combine(root, "small.jpg");
        ImageFixtureFactory.CreateJpeg(sourcePath, width: 20, height: 10);
        await using var destination = new MemoryStream();
        var generator = Generator(root);

        await generator.WriteWebPAsync(Photo(root, sourcePath), destination, maxLongEdge: 400, quality: 80, TestContext.Current.CancellationToken);

        destination.Position = 0;
        using var derivative = await Image.LoadAsync(destination, TestContext.Current.CancellationToken);
        Assert.Equal(20, derivative.Width);
        Assert.Equal(10, derivative.Height);
    }

    [Fact]
    public async Task WriteWebPAsync_rejects_an_indexed_photo_that_the_source_guard_does_not_trust()
    {
        using var directory = new TemporaryDirectory();
        var root = directory.CreateDirectory("photos");
        var sourcePath = Path.Combine(root, "changed.jpg");
        ImageFixtureFactory.CreateJpeg(sourcePath);
        var indexedPhoto = Photo(root, sourcePath);
        File.AppendAllText(sourcePath, "changed after indexing");
        await using var destination = new MemoryStream();

        await Assert.ThrowsAsync<StaleSourceException>(() =>
            Generator(root).WriteWebPAsync(indexedPhoto, destination, maxLongEdge: 50, quality: 80, TestContext.Current.CancellationToken));

        Assert.Equal(0, destination.Length);
    }

    [Fact]
    public async Task WriteWebPAsync_rejects_a_malformed_guarded_source_without_writing_output()
    {
        using var directory = new TemporaryDirectory();
        var root = directory.CreateDirectory("photos");
        var sourcePath = Path.Combine(root, "not-an-image.jpg");
        File.WriteAllText(sourcePath, "not an image");
        await using var destination = new MemoryStream();

        await Assert.ThrowsAsync<UnknownImageFormatException>(() =>
            Generator(root).WriteWebPAsync(Photo(root, sourcePath), destination, maxLongEdge: 50, quality: 80, TestContext.Current.CancellationToken));

        Assert.Equal(0, destination.Length);
    }

    [Fact]
    public async Task WriteWebPAsync_translates_an_expected_read_failure_to_stale_when_the_source_is_no_longer_current()
    {
        using var directory = new TemporaryDirectory();
        var root = directory.CreateDirectory("photos");
        var sourcePath = Path.Combine(root, "removed.jpg");
        ImageFixtureFactory.CreateJpeg(sourcePath);
        var photo = Photo(root, sourcePath);
        var guard = new RemoveSourceAfterFirstResolutionGuard(
            new SourcePathGuard(new FolderFolioOptions { PhotoRoot = root }),
            sourcePath);
        await using var destination = new MemoryStream();

        await Assert.ThrowsAsync<StaleSourceException>(() =>
            new ImageSharpDerivativeGenerator(guard).WriteWebPAsync(
                photo,
                destination,
                maxLongEdge: 50,
                quality: 80,
                TestContext.Current.CancellationToken));

        Assert.Equal(0, destination.Length);
    }

    private static ImageSharpDerivativeGenerator Generator(string root) =>
        new(new SourcePathGuard(new FolderFolioOptions { PhotoRoot = root }));

    private static IndexedPhoto Photo(string root, string sourcePath)
    {
        var info = new FileInfo(sourcePath);
        return new IndexedPhoto(
            "photo-id",
            Path.GetFileName(sourcePath),
            new SourceFingerprint(Path.GetRelativePath(root, sourcePath), info.Length, info.LastWriteTimeUtc.Ticks),
            null,
            120,
            80);
    }

    private sealed class RemoveSourceAfterFirstResolutionGuard(ISourcePathGuard inner, string sourcePath) : ISourcePathGuard
    {
        private int resolutionCount;

        public bool TryResolve(IndexedPhoto photo, out string resolvedPath)
        {
            var resolved = inner.TryResolve(photo, out resolvedPath);
            if (Interlocked.Increment(ref resolutionCount) == 1 && resolved)
            {
                File.Delete(sourcePath);
            }

            return resolved;
        }
    }
}

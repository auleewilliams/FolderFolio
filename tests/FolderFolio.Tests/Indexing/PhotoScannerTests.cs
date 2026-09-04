using FolderFolio.Configuration;
using FolderFolio.Domain;
using FolderFolio.Indexing;
using FolderFolio.Tests.Support;
using Xunit;

namespace FolderFolio.Tests.Indexing;

public sealed class PhotoScannerTests
{
    [Fact]
    public async Task ScanAllAsync_indexes_only_immediate_supported_files_in_album_and_photo_order()
    {
        using var directory = new TemporaryDirectory();
        var trip = directory.CreateDirectory("02-Trip");
        var portraits = directory.CreateDirectory("Portraits");
        ImageFixtureFactory.CreateJpeg(Path.Combine(trip, "early.jpg"), dateTimeOriginal: "2024:01:01 01:00:00");
        ImageFixtureFactory.CreateJpeg(Path.Combine(trip, "same-a.jpg"), dateTimeOriginal: "2024:01:02 01:00:00");
        ImageFixtureFactory.CreateJpeg(Path.Combine(trip, "same-b.jpg"), dateTimeOriginal: "2024:01:02 01:00:00");
        ImageFixtureFactory.CreateJpeg(Path.Combine(trip, "undated.jpg"), dateTimeOriginal: null);
        ImageFixtureFactory.CreateJpeg(Path.Combine(portraits, "portrait.jpg"));
        File.WriteAllText(Path.Combine(trip, "notes.txt"), "not an image");
        File.WriteAllText(Path.Combine(trip, "corrupt.jpg"), "not a jpeg");
        var nested = Directory.CreateDirectory(Path.Combine(trip, "nested"));
        ImageFixtureFactory.CreateJpeg(Path.Combine(nested.FullName, "ignored.jpg"));
        try
        {
            File.CreateSymbolicLink(Path.Combine(trip, "linked.jpg"), Path.Combine(trip, "early.jpg"));
        }
        catch (IOException exception) when (OperatingSystem.IsWindows())
        {
            throw Xunit.Sdk.SkipException.ForSkip($"Windows symlink creation is unavailable: {exception.Message}");
        }
        var scanner = Scanner(directory.Path);

        var result = await scanner.ScanAllAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["02-Trip", "Portraits"], result.Snapshot.Albums.Select(album => album.DirectoryName));
        var album = result.Snapshot.Albums[0];
        Assert.Equal(["early.jpg", "same-a.jpg", "same-b.jpg", "undated.jpg"], album.Photos.Select(photo => photo.FileName));
        Assert.Equal(album.Photos[0], album.Cover);
        Assert.Equal(120, album.Photos[0].Width);
        Assert.Equal(80, album.Photos[0].Height);
        Assert.Equal("02-Trip/early.jpg", album.Photos[0].Source.RelativePath);
        Assert.False(Path.IsPathRooted(album.Photos[0].Source.RelativePath));
        Assert.True(album.Photos[0].Source.Length > 0);
        Assert.True(album.Photos[0].Source.LastWriteUtcTicks > 0);
        Assert.Equal(4, result.SkippedFileCount);
        Assert.DoesNotContain(album.Photos, photo => photo.FileName is "ignored.jpg" or "linked.jpg");
    }

    [Fact]
    public async Task RescanAlbumsAsync_replaces_changed_album_and_retains_untouched_albums()
    {
        using var directory = new TemporaryDirectory();
        var trip = directory.CreateDirectory("01-Trip");
        var portraits = directory.CreateDirectory("Portraits");
        ImageFixtureFactory.CreateJpeg(Path.Combine(trip, "before.jpg"));
        ImageFixtureFactory.CreateJpeg(Path.Combine(portraits, "kept.jpg"));
        var scanner = Scanner(directory.Path);
        var initial = await scanner.ScanAllAsync(TestContext.Current.CancellationToken);
        File.Delete(Path.Combine(trip, "before.jpg"));
        ImageFixtureFactory.CreateJpeg(Path.Combine(trip, "after.jpg"));

        var rescanned = await scanner.RescanAlbumsAsync(
            initial.Snapshot,
            new HashSet<string>(StringComparer.Ordinal) { "01-Trip" },
            TestContext.Current.CancellationToken);

        Assert.Equal(["01-Trip", "Portraits"], rescanned.Snapshot.Albums.Select(album => album.DirectoryName));
        Assert.Equal(["after.jpg"], rescanned.Snapshot.Albums[0].Photos.Select(photo => photo.FileName));
        Assert.Equal(initial.Snapshot.Albums[1], rescanned.Snapshot.Albums[1]);
    }

    [Fact]
    public async Task RescanAlbumsAsync_removes_a_targeted_album_that_no_longer_exists()
    {
        using var directory = new TemporaryDirectory();
        var trip = directory.CreateDirectory("01-Trip");
        var portraits = directory.CreateDirectory("Portraits");
        ImageFixtureFactory.CreateJpeg(Path.Combine(trip, "trip.jpg"));
        ImageFixtureFactory.CreateJpeg(Path.Combine(portraits, "portrait.jpg"));
        var scanner = Scanner(directory.Path);
        var initial = await scanner.ScanAllAsync(TestContext.Current.CancellationToken);
        Directory.Delete(trip, recursive: true);

        var rescanned = await scanner.RescanAlbumsAsync(
            initial.Snapshot,
            new HashSet<string>(StringComparer.Ordinal) { "01-Trip" },
            TestContext.Current.CancellationToken);

        Assert.Equal(["Portraits"], rescanned.Snapshot.Albums.Select(album => album.DirectoryName));
    }

    [Theory]
    [InlineData("../Portraits")]
    [InlineData("one/two")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("/absolute")]
    public async Task RescanAlbumsAsync_rejects_unsafe_album_directory_names(string directoryName)
    {
        using var directory = new TemporaryDirectory();
        var scanner = Scanner(directory.Path);

        await Assert.ThrowsAsync<ArgumentException>(() => scanner.RescanAlbumsAsync(
            PortfolioSnapshot.Empty,
            new HashSet<string>(StringComparer.Ordinal) { directoryName },
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ScanAllAsync_skips_files_that_change_while_metadata_is_read()
    {
        using var directory = new TemporaryDirectory();
        var album = directory.CreateDirectory("Trip");
        var path = Path.Combine(album, "changing.jpg");
        ImageFixtureFactory.CreateJpeg(path);
        var scanner = Scanner(directory.Path, new ChangingMetadataReader(path));

        var result = await scanner.ScanAllAsync(TestContext.Current.CancellationToken);

        Assert.Empty(result.Snapshot.Albums[0].Photos);
        Assert.Equal(1, result.SkippedFileCount);
    }

    [Fact]
    public async Task ScanAllAsync_skips_images_over_the_decoded_pixel_budget()
    {
        using var directory = new TemporaryDirectory();
        var album = directory.CreateDirectory("Trip");
        ImageFixtureFactory.CreateJpeg(Path.Combine(album, "large.jpg"));
        var scanner = Scanner(directory.Path, new OversizedMetadataReader());

        var result = await scanner.ScanAllAsync(TestContext.Current.CancellationToken);

        Assert.Empty(result.Snapshot.Albums[0].Photos);
        Assert.Equal(1, result.SkippedFileCount);
    }

    [Fact]
    public async Task ScanAllAsync_accepts_only_the_four_contract_image_extensions_case_insensitively()
    {
        using var directory = new TemporaryDirectory();
        var album = directory.CreateDirectory("Formats");
        foreach (var fileName in new[] { "one.JpG", "two.jPeG", "three.PnG", "four.WeBp" })
        {
            ImageFixtureFactory.CreateJpeg(Path.Combine(album, fileName));
        }

        foreach (var fileName in new[] { "skip.BmP", "skip.GiF", "skip.TiF", "skip.TiFf" })
        {
            ImageFixtureFactory.CreateJpeg(Path.Combine(album, fileName));
        }

        var result = await Scanner(directory.Path).ScanAllAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["four.WeBp", "one.JpG", "three.PnG", "two.jPeG"], result.Snapshot.Albums[0].Photos.Select(photo => photo.FileName));
        Assert.Equal(4, result.SkippedFileCount);
    }

    [Fact]
    public async Task ScanAllAsync_skips_unix_source_file_names_containing_backslashes()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var directory = new TemporaryDirectory();
        var album = directory.CreateDirectory("Backslashes");
        ImageFixtureFactory.CreateJpeg(Path.Combine(album, "literal\\name.jpg"));

        var result = await Scanner(directory.Path).ScanAllAsync(TestContext.Current.CancellationToken);

        Assert.Empty(result.Snapshot.Albums[0].Photos);
        Assert.Equal(1, result.SkippedFileCount);
    }

    [Fact]
    public async Task ScanAllAsync_omits_an_album_whose_directory_enumeration_fails_and_keeps_other_albums()
    {
        using var directory = new TemporaryDirectory();
        var missing = directory.CreateDirectory("Missing");
        var retained = directory.CreateDirectory("Retained");
        ImageFixtureFactory.CreateJpeg(Path.Combine(missing, "missing.jpg"));
        ImageFixtureFactory.CreateJpeg(Path.Combine(retained, "retained.jpg"));

        var result = await Scanner(directory.Path, fileSystem: new ThrowingPhotoScanFileSystem(missing))
            .ScanAllAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["Retained"], result.Snapshot.Albums.Select(album => album.DirectoryName));
    }

    [Fact]
    public async Task RescanAlbumsAsync_removes_targeted_album_when_its_directory_enumeration_fails()
    {
        using var directory = new TemporaryDirectory();
        var missing = directory.CreateDirectory("Missing");
        var retained = directory.CreateDirectory("Retained");
        ImageFixtureFactory.CreateJpeg(Path.Combine(missing, "missing.jpg"));
        ImageFixtureFactory.CreateJpeg(Path.Combine(retained, "retained.jpg"));
        var initial = await Scanner(directory.Path).ScanAllAsync(TestContext.Current.CancellationToken);

        var result = await Scanner(directory.Path, fileSystem: new ThrowingPhotoScanFileSystem(missing)).RescanAlbumsAsync(
            initial.Snapshot,
            new HashSet<string>(StringComparer.Ordinal) { "Missing" },
            TestContext.Current.CancellationToken);

        Assert.Equal(["Retained"], result.Snapshot.Albums.Select(album => album.DirectoryName));
    }

    private static PhotoScanner Scanner(
        string photoRoot,
        IImageMetadataReader? metadataReader = null,
        IPhotoScanFileSystem? fileSystem = null) =>
        new(
            new FolderFolioOptions { PhotoRoot = photoRoot, CacheRoot = photoRoot },
            metadataReader ?? new ImageSharpMetadataReader(),
            fileSystem: fileSystem);

    private sealed class ChangingMetadataReader(string path) : IImageMetadataReader
    {
        public Task<PhotoSourceMetadata> IdentifyAsync(string sourcePath, CancellationToken cancellationToken)
        {
            File.AppendAllText(path, "changed");
            return Task.FromResult(new PhotoSourceMetadata(120, 80, null, 38_400));
        }
    }

    private sealed class OversizedMetadataReader : IImageMetadataReader
    {
        public Task<PhotoSourceMetadata> IdentifyAsync(string sourcePath, CancellationToken cancellationToken) =>
            Task.FromResult(new PhotoSourceMetadata(120, 80, null, (512L * 1024 * 1024) + 1));
    }

    private sealed class ThrowingPhotoScanFileSystem(string failingDirectory) : IPhotoScanFileSystem
    {
        public bool DirectoryExists(string path) => Directory.Exists(path);

        public IEnumerable<string> EnumerateDirectories(string path) =>
            Directory.EnumerateDirectories(path, "*", SearchOption.TopDirectoryOnly);

        public IEnumerable<string> EnumerateFiles(string path)
        {
            if (string.Equals(path, failingDirectory, StringComparison.Ordinal))
            {
                throw new DirectoryNotFoundException("Album disappeared.");
            }

            return Directory.EnumerateFiles(path, "*", SearchOption.TopDirectoryOnly);
        }

        public FileAttributes GetAttributes(string path) => File.GetAttributes(path);
    }
}

using FolderFolio.Configuration;
using FolderFolio.Domain;
using FolderFolio.Imaging;
using FolderFolio.Tests.Support;
using Xunit;

namespace FolderFolio.Tests.Imaging;

public sealed class SourcePathGuardTests
{
    [Fact]
    public void TryResolve_returns_the_canonical_file_for_a_matching_photo_under_the_trusted_root()
    {
        using var directory = new TemporaryDirectory();
        var root = directory.CreateDirectory("photos");
        var path = Path.Combine(root, "album", "photo.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "photo");
        var photo = Photo("album/photo.jpg", path);

        var resolved = new SourcePathGuard(new FolderFolioOptions { PhotoRoot = root }).TryResolve(photo, out var sourcePath);

        Assert.True(resolved);
        Assert.Equal(Path.GetFullPath(path), sourcePath);
    }

    [Theory]
    [InlineData("../outside.jpg")]
    [InlineData("/outside.jpg")]
    public void TryResolve_rejects_a_path_that_does_not_stay_under_the_trusted_root(string relativePath)
    {
        using var directory = new TemporaryDirectory();
        var root = directory.CreateDirectory("photos");
        var outside = directory.FilePath("outside.jpg");
        File.WriteAllText(outside, "photo");

        var resolved = new SourcePathGuard(new FolderFolioOptions { PhotoRoot = root }).TryResolve(Photo(relativePath, outside), out var sourcePath);

        Assert.False(resolved);
        Assert.Equal(string.Empty, sourcePath);
    }

    [Fact]
    public void TryResolve_rejects_a_symlink_source()
    {
        using var directory = new TemporaryDirectory();
        var root = directory.CreateDirectory("photos");
        var target = directory.FilePath("target.jpg");
        var link = Path.Combine(root, "linked.jpg");
        File.WriteAllText(target, "photo");
        try
        {
            File.CreateSymbolicLink(link, target);
        }
        catch (IOException exception) when (OperatingSystem.IsWindows())
        {
            throw Xunit.Sdk.SkipException.ForSkip($"Windows symlink creation is unavailable: {exception.Message}");
        }

        var resolved = new SourcePathGuard(new FolderFolioOptions { PhotoRoot = root }).TryResolve(Photo("linked.jpg", target), out _);

        Assert.False(resolved);
    }

    [Fact]
    public void TryResolve_rejects_missing_and_changed_fingerprint_sources()
    {
        using var directory = new TemporaryDirectory();
        var root = directory.CreateDirectory("photos");
        var path = Path.Combine(root, "photo.jpg");
        File.WriteAllText(path, "photo");
        var guard = new SourcePathGuard(new FolderFolioOptions { PhotoRoot = root });
        var matching = Photo("photo.jpg", path);

        File.Delete(path);
        Assert.False(guard.TryResolve(matching, out _));

        File.WriteAllText(path, "changed photo");
        Assert.False(guard.TryResolve(matching, out _));

        var current = Photo("photo.jpg", path);
        File.SetLastWriteTimeUtc(path, new DateTime(current.Source.LastWriteUtcTicks + TimeSpan.TicksPerSecond, DateTimeKind.Utc));
        Assert.False(guard.TryResolve(current, out _));
    }

    private static IndexedPhoto Photo(string relativePath, string sourcePath)
    {
        var info = new FileInfo(sourcePath);
        return new IndexedPhoto("photo-id", Path.GetFileName(relativePath), new SourceFingerprint(relativePath, info.Length, info.LastWriteTimeUtc.Ticks), null, 120, 80);
    }
}

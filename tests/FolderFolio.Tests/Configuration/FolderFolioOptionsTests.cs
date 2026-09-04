using FolderFolio.Configuration;
using Xunit;

namespace FolderFolio.Tests.Configuration;

public sealed class FolderFolioOptionsTests
{
    [Fact]
    public void Defaults_match_the_container_contract()
    {
        var options = new FolderFolioOptions();

        Assert.Equal("/photos", options.PhotoRoot);
        Assert.Equal("/cache", options.CacheRoot);
        Assert.Equal(400, options.GridLongEdge);
        Assert.Equal(2000, options.WebLongEdge);
        Assert.Equal(82, options.WebPQuality);
        Assert.Equal("FolderFolio", options.SiteTitle);
    }

    [Fact]
    public void Validator_rejects_invalid_paths_sizes_quality_and_title()
    {
        var options = new FolderFolioOptions
        {
            PhotoRoot = "relative/photos",
            CacheRoot = " ",
            GridLongEdge = 0,
            WebLongEdge = -1,
            WebPQuality = 101,
            SiteTitle = " "
        };

        var result = new FolderFolioOptionsValidator().Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, value => value.Contains(nameof(options.PhotoRoot)));
        Assert.Contains(result.Failures!, value => value.Contains(nameof(options.CacheRoot)));
        Assert.Contains(result.Failures!, value => value.Contains(nameof(options.GridLongEdge)));
        Assert.Contains(result.Failures!, value => value.Contains(nameof(options.WebPQuality)));
        Assert.Contains(result.Failures!, value => value.Contains(nameof(options.SiteTitle)));
    }

    [Fact]
    public void Validator_rejects_web_long_edge_smaller_than_grid_long_edge()
    {
        var options = new FolderFolioOptions
        {
            GridLongEdge = 401,
            WebLongEdge = 400
        };

        var result = new FolderFolioOptionsValidator().Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, value => value.Contains(nameof(options.WebLongEdge)));
    }

    [Fact]
    public void Validator_accepts_a_valid_configuration()
    {
        var fullyQualifiedRoot = Path.GetFullPath(Path.GetTempPath());
        var result = new FolderFolioOptionsValidator().Validate(null, new FolderFolioOptions
        {
            PhotoRoot = fullyQualifiedRoot,
            CacheRoot = fullyQualifiedRoot
        });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validator_rejects_windows_drive_relative_photo_and_cache_roots()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw Xunit.Sdk.SkipException.ForSkip("Drive-relative rooted paths are a Windows path form.");
        }

        var options = new FolderFolioOptions
        {
            PhotoRoot = @"C:photos",
            CacheRoot = @"D:cache"
        };

        var result = new FolderFolioOptionsValidator().Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, value => value.Contains(nameof(options.PhotoRoot), StringComparison.Ordinal));
        Assert.Contains(result.Failures!, value => value.Contains(nameof(options.CacheRoot), StringComparison.Ordinal));
    }
}

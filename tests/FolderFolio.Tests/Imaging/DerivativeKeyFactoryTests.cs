using FolderFolio.Configuration;
using FolderFolio.Domain;
using FolderFolio.Imaging;
using Xunit;

namespace FolderFolio.Tests.Imaging;

public sealed class DerivativeKeyFactoryTests
{
    [Fact]
    public void Create_is_deterministic_and_exposes_a_lowercase_url_version_and_quoted_etag()
    {
        var factory = Factory();
        var photo = Photo();

        var first = factory.Create(photo, DerivativeKind.Grid);
        var second = factory.Create(photo, DerivativeKind.Grid);

        Assert.Equal(first, second);
        Assert.Matches("^[0-9a-f]{64}$", first.CacheKey);
        Assert.Equal(first.CacheKey[..24], first.Version);
        Assert.Matches("^[0-9a-f]{24}$", first.Version);
        Assert.Equal($"\"{first.CacheKey}\"", first.ETag);
        Assert.Equal(400, first.MaxLongEdge);
        Assert.Equal(82, first.WebPQuality);
    }

    [Fact]
    public void Create_changes_identity_when_source_or_derivative_inputs_change()
    {
        var factory = Factory();
        var photo = Photo();
        var baseline = factory.Create(photo, DerivativeKind.Grid);

        var variants = new[]
        {
            factory.Create(photo with { Source = photo.Source with { RelativePath = "Album/renamed.jpg" } }, DerivativeKind.Grid),
            factory.Create(photo with { Source = photo.Source with { Length = 124L } }, DerivativeKind.Grid),
            factory.Create(photo with { Source = photo.Source with { LastWriteUtcTicks = 638_400_000_000_000_001L } }, DerivativeKind.Grid),
            factory.Create(photo, DerivativeKind.Web),
            new DerivativeKeyFactory(new FolderFolioOptions { GridLongEdge = 401, WebLongEdge = 2000, WebPQuality = 82 }).Create(photo, DerivativeKind.Grid),
            new DerivativeKeyFactory(new FolderFolioOptions { GridLongEdge = 400, WebLongEdge = 2000, WebPQuality = 83 }).Create(photo, DerivativeKind.Grid),
            new DerivativeKeyFactory(new FolderFolioOptions { GridLongEdge = 400, WebLongEdge = 2000, WebPQuality = 82 }, cacheSchema: 2).Create(photo, DerivativeKind.Grid)
        };

        Assert.All(variants, identity =>
        {
            Assert.NotEqual(baseline.CacheKey, identity.CacheKey);
            Assert.NotEqual(baseline.Version, identity.Version);
        });
    }

    private static DerivativeKeyFactory Factory() =>
        new(new FolderFolioOptions { GridLongEdge = 400, WebLongEdge = 2000, WebPQuality = 82 });

    private static IndexedPhoto Photo() =>
        new("photo-id", "photo.jpg", new SourceFingerprint("Album/photo.jpg", 123L, 638_400_000_000_000_000L), null, 120, 80);
}

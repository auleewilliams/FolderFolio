using System.Collections.Immutable;
using FolderFolio.Domain;
using FolderFolio.Imaging;
using FolderFolio.Indexing;
using FolderFolio.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FolderFolio.Tests.Web;

public sealed class MediaEndpointTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();
    private readonly TemporaryDirectory _photoRoot = new();

    [Fact]
    public async Task A_current_version_returns_the_webp_with_immutable_cache_metadata()
    {
        var (album, photo, derivative, factory) = CreateFixture();
        using var app = new FolderFolioWebApplicationFactory(new PortfolioSnapshot(ImmutableArray.Create(album)), derivative, _photoRoot.Path);
        using var client = app.CreateClient();
        var version = factory.Create(photo, DerivativeKind.Grid).Version;

        var response = await client.GetAsync($"/media/landscapes/{Uri.EscapeDataString(photo.Id)}/grid?v={version}", TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(_photoRoot.Path, app.Services.GetRequiredService<FolderFolio.Configuration.FolderFolioOptions>().PhotoRoot);
        Assert.Equal("image/webp", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(factory.Create(photo, DerivativeKind.Grid).ETag, response.Headers.ETag?.Tag);
        Assert.Equal("public,max-age=31536000,immutable", response.Headers.CacheControl?.ToString().Replace(" ", string.Empty));
        await AssertDoesNotDisclosePhotoRoot(response);
    }

    [Theory]
    [InlineData("unknown", "photo-id", "grid", "version")]
    [InlineData("landscapes", "unknown", "grid", "version")]
    [InlineData("landscapes", "photo-id", "thumbnail", "version")]
    [InlineData("landscapes", "photo-id", "grid", "")]
    [InlineData("landscapes", "photo-id", "grid", "stale")]
    public async Task An_unresolvable_media_lookup_returns_not_found(string albumSlug, string photoId, string size, string version)
    {
        var (album, _, derivative, _) = CreateFixture();
        using var app = new FolderFolioWebApplicationFactory(new PortfolioSnapshot(ImmutableArray.Create(album)), derivative, _photoRoot.Path);
        using var client = app.CreateClient();
        var query = version.Length == 0 ? string.Empty : $"?v={version}";

        var response = await client.GetAsync($"/media/{albumSlug}/{photoId}/{size}{query}", TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
        await AssertDoesNotDisclosePhotoRoot(response);
    }

    [Fact]
    public async Task A_stale_source_queues_its_indexed_album_and_returns_not_found()
    {
        var (album, photo, derivative, factory) = CreateFixture();
        derivative.ThrowStaleSource = true;
        using var app = new FolderFolioWebApplicationFactory(new PortfolioSnapshot(ImmutableArray.Create(album)), derivative, _photoRoot.Path);
        using var client = app.CreateClient();

        var response = await client.GetAsync($"/media/landscapes/{photo.Id}/grid?v={factory.Create(photo, DerivativeKind.Grid).Version}", TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
        await AssertDoesNotDisclosePhotoRoot(response);
        Assert.True(app.RefreshQueue.TryRead(out var request));
        Assert.False(request.FullScan);
        Assert.Equal([album.DirectoryName], request.AlbumDirectoryNames);
    }

    public void Dispose()
    {
        _photoRoot.Dispose();
        _directory.Dispose();
    }

    private async Task AssertDoesNotDisclosePhotoRoot(HttpResponseMessage response)
    {
        var responseText = $"{response}\n{await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)}";

        Assert.DoesNotContain(
            _photoRoot.Path,
            responseText,
            StringComparison.Ordinal);
    }

    private (IndexedAlbum Album, IndexedPhoto Photo, StubDerivativeService Derivative, DerivativeKeyFactory KeyFactory) CreateFixture()
    {
        var derivativePath = Path.Combine(_directory.Path, "cached.webp");
        File.WriteAllBytes(derivativePath, [1, 2, 3]);
        var lastModified = new DateTimeOffset(2026, 9, 3, 0, 0, 0, TimeSpan.Zero);
        File.SetLastWriteTimeUtc(derivativePath, lastModified.UtcDateTime);
        var photo = new IndexedPhoto("photo-id", "coast.jpg", new SourceFingerprint("01 Landscapes/coast.jpg", 3, 123), null, 120, 80);
        var album = new IndexedAlbum("01 Landscapes", "landscapes", "Landscapes", "landscapes", 1, ImmutableArray.Create(photo));
        return (album, photo, new StubDerivativeService { Derivative = new CachedDerivative(derivativePath, 3, lastModified) }, new DerivativeKeyFactory(new FolderFolio.Configuration.FolderFolioOptions()));
    }
}

using System.Net;
using FolderFolio.Configuration;
using FolderFolio.Imaging;
using FolderFolio.Indexing;
using FolderFolio.Tests.Support;
using ImageMagick;
using Xunit;

namespace FolderFolio.Tests.Web;

public sealed class AvifEndpointTests
{
    [Fact]
    public async Task Indexed_AVIF_sources_produce_real_cached_AVIF_and_WebP_HTTP_responses()
    {
        using var root = new TemporaryDirectory();
        using var cache = new TemporaryDirectory();
        var albumPath = root.CreateDirectory("01-Album");
        var path = Path.Combine(albumPath, "photo.avif");
        using (var image = new MagickImage(MagickColors.Red, 120, 80)) image.Write(path, MagickFormat.Avif);
        var options = new FolderFolioOptions { PhotoRoot = root.Path, CacheRoot = cache.Path };
        var snapshot = (await new PhotoScanner(options, new ImageSharpMetadataReader()).ScanAllAsync(TestContext.Current.CancellationToken)).Snapshot;
        var photo = Assert.Single(snapshot.Albums[0].Photos);
        var keys = new DerivativeKeyFactory(options);
        var guard = new SourcePathGuard(options);
        var service = new DerivativeService(keys, guard, new ImageSharpDerivativeGenerator(guard), options, new TestHostApplicationLifetime());
        using var app = new FolderFolioWebApplicationFactory(snapshot, service, root.Path);
        using var client = app.CreateClient();

        foreach (var (kind, size, mime, format) in new[]
        {
            (DerivativeKind.Avif, "avif", "image/avif", MagickFormat.Avif),
            (DerivativeKind.Grid, "grid", "image/webp", MagickFormat.WebP)
        })
        {
            var identity = keys.Create(photo, kind);
            var url = $"/media/album/{photo.Id}/{size}?v={identity.Version}";
            using var response = await client.GetAsync(url, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(mime, response.Content.Headers.ContentType?.MediaType);
            Assert.Equal(identity.ETag, response.Headers.ETag?.Tag);
            Assert.Contains("immutable", response.Headers.CacheControl!.ToString());
            using var decoded = new MagickImage(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
            Assert.Equal(format, decoded.Format);
            Assert.Equal(120u, decoded.Width);
            Assert.Equal(80u, decoded.Height);
            using var conditional = new HttpRequestMessage(HttpMethod.Get, url);
            conditional.Headers.IfNoneMatch.Add(response.Headers.ETag!);
            using var unchanged = await client.SendAsync(conditional, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NotModified, unchanged.StatusCode);
        }

        Assert.Single(Directory.EnumerateFiles(cache.Path, "*.avif", SearchOption.AllDirectories));
        Assert.Single(Directory.EnumerateFiles(cache.Path, "*.webp", SearchOption.AllDirectories));
        using var mismatched = await client.GetAsync($"/media/album/{photo.Id}/avif?v={keys.Create(photo, DerivativeKind.Web).Version}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, mismatched.StatusCode);
    }
}

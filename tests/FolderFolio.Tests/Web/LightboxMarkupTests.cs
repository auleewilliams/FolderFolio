using System.Collections.Immutable;
using FolderFolio.Domain;
using FolderFolio.Imaging;
using FolderFolio.Tests.Support;
using Xunit;

namespace FolderFolio.Tests.Web;

public sealed class LightboxMarkupTests
{
    [Fact]
    public async Task Album_page_includes_one_labelled_native_dialog_and_its_controls()
    {
        using var root = new TemporaryDirectory();
        var photo = new IndexedPhoto("photo", "coast.jpg", new SourceFingerprint("01-Landscapes/coast.jpg", 1, 1), null, 120, 80);
        var album = new IndexedAlbum("01-Landscapes", "landscapes", "Landscapes", "landscapes", 1, ImmutableArray.Create(photo));
        using var app = new FolderFolioWebApplicationFactory(
            new PortfolioSnapshot(ImmutableArray.Create(album)),
            new StubDerivativeService { Derivative = new CachedDerivative("unused", 0, DateTimeOffset.UnixEpoch) },
            root.Path);
        using var client = app.CreateClient();

        var response = await client.GetAsync("/albums/landscapes", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        Assert.Equal(1, html.Split("<dialog id=\"photo-lightbox\"", StringSplitOptions.None).Length - 1);
        Assert.Contains("aria-labelledby=\"lightbox-title\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"lightbox-title\"", html, StringComparison.Ordinal);
        Assert.Contains("data-lightbox-close", html, StringComparison.Ordinal);
        Assert.Contains("data-lightbox-previous", html, StringComparison.Ordinal);
        Assert.Contains("data-lightbox-next", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Close photo\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Previous photo\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Next photo\"", html, StringComparison.Ordinal);
        Assert.Contains("data-lightbox-image", html, StringComparison.Ordinal);
        Assert.Contains("data-state=\"idle\"", html, StringComparison.Ordinal);
        Assert.Contains("/js/lightbox.js", html, StringComparison.Ordinal);
    }
}

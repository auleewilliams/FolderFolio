using System.Collections.Immutable;
using FolderFolio.Domain;
using FolderFolio.Imaging;
using FolderFolio.Indexing;
using FolderFolio.Pages.Albums;
using FolderFolio.Tests.Support;
using FolderFolio.Web;
using FolderFolio.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Xunit;

namespace FolderFolio.Tests.Web;

public sealed class AlbumDetailsPageTests
{
    [Fact]
    public void OnGet_maps_positions_dimensions_accessible_labels_and_current_media_urls()
    {
        var model = new DetailsModel(ReadyIndex(Snapshot()), ViewModels());

        var result = model.OnGet("landscapes");

        Assert.IsType<PageResult>(result);
        Assert.False(model.IsPreparing);
        var gallery = Assert.IsType<AlbumGalleryViewModel>(model.Gallery);
        var first = gallery.Photos[0];
        Assert.Equal(1, first.Position);
        Assert.Equal(120, first.Width);
        Assert.Equal(80, first.Height);
        Assert.Equal("Photo 1 of 2 in Landscapes", first.AccessibleName);
        Assert.Equal("/media/landscapes/first/grid?v=current", first.GridUrl);
        Assert.Equal("/media/landscapes/first/web?v=current", first.WebUrl);
        Assert.True(first.LoadEagerly);
        Assert.True(gallery.Photos[1].LoadEagerly);
    }

    [Fact]
    public void OnGet_marks_only_the_first_four_photos_for_eager_loading()
    {
        var model = new DetailsModel(ReadyIndex(Snapshot(5)), ViewModels());

        model.OnGet("landscapes");

        var gallery = Assert.IsType<AlbumGalleryViewModel>(model.Gallery);
        Assert.All(gallery.Photos.Take(4), photo => Assert.True(photo.LoadEagerly));
        Assert.False(gallery.Photos[4].LoadEagerly);
    }

    [Fact]
    public void OnGet_returns_not_found_for_an_unknown_album_after_a_successful_scan()
    {
        var model = new DetailsModel(ReadyIndex(Snapshot()), ViewModels());

        var result = model.OnGet("missing");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void OnGet_renders_preparing_when_no_successful_snapshot_exists()
    {
        var model = new DetailsModel(new PortfolioIndex(), ViewModels());

        var result = model.OnGet("landscapes");

        Assert.IsType<PageResult>(result);
        Assert.True(model.IsPreparing);
        Assert.Null(model.Gallery);
    }

    [Fact]
    public async Task Album_page_renders_lightbox_data_and_an_accessible_image_failure_fallback()
    {
        using var root = new TemporaryDirectory();
        using var app = new FolderFolioWebApplicationFactory(
            Snapshot(),
            new StubDerivativeService { Derivative = new CachedDerivative("unused", 0, DateTimeOffset.UnixEpoch) },
            root.Path);
        using var client = app.CreateClient();

        var response = await client.GetAsync("/albums/landscapes", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        Assert.Equal(2, Count(html, "data-lightbox-trigger"));
        Assert.Contains("data-web-src=\"/media/landscapes/first/web?v=", html, StringComparison.Ordinal);
        Assert.Contains("data-alt=\"Photo 1 of 2 in Landscapes\"", html, StringComparison.Ordinal);
        Assert.Contains("data-index=\"0\"", html, StringComparison.Ordinal);
        Assert.Contains("width=\"120\"", html, StringComparison.Ordinal);
        Assert.Contains("height=\"80\"", html, StringComparison.Ordinal);
        Assert.Contains("Image unavailable", html, StringComparison.Ordinal);
        Assert.DoesNotContain("01-Landscapes/first.jpg", html, StringComparison.Ordinal);
    }

    private static int Count(string value, string fragment) => value.Split(fragment, StringSplitOptions.None).Length - 1;

    private static PortfolioSnapshot Snapshot(int photoCount = 2) => new(ImmutableArray.Create(new IndexedAlbum(
        "01-Landscapes",
        "landscapes",
        "Landscapes",
        "landscapes",
        1,
        Enumerable.Range(1, photoCount)
            .Select(position => new IndexedPhoto(
                position == 1 ? "first" : position == 2 ? "second" : $"photo-{position}",
                $"{position}.jpg",
                new SourceFingerprint($"01-Landscapes/{position}.jpg", 1, position),
                null,
                position == 1 ? 120 : 60,
                position == 1 ? 80 : 90))
            .ToImmutableArray())));

    private static PortfolioViewModelFactory ViewModels() => new(new StubMediaUrlBuilder());

    private static PortfolioIndex ReadyIndex(PortfolioSnapshot snapshot)
    {
        var index = new PortfolioIndex();
        index.PublishReady(snapshot, DateTimeOffset.UnixEpoch, TimeSpan.Zero);
        return index;
    }

    private sealed class StubMediaUrlBuilder : IMediaUrlBuilder
    {
        public string Build(IndexedAlbum album, IndexedPhoto photo, DerivativeKind kind) =>
            $"/media/{album.Slug}/{photo.Id}/{kind.ToString().ToLowerInvariant()}?v=current";
    }
}

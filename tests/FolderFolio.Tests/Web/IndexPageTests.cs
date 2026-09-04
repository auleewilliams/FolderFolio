using System.Collections.Immutable;
using FolderFolio.Configuration;
using FolderFolio.Domain;
using FolderFolio.Indexing;
using FolderFolio.Imaging;
using FolderFolio.Pages;
using FolderFolio.Tests.Support;
using FolderFolio.Web;
using FolderFolio.Web.ViewModels;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Xunit;

namespace FolderFolio.Tests.Web;

public sealed class IndexPageTests
{
    [Fact]
    public void OnGet_maps_site_details_and_indexed_albums_in_snapshot_order()
    {
        var index = ReadyIndex(Snapshot());
        var model = new IndexModel(index, Options(), ViewModels());

        model.OnGet();

        Assert.Equal("Gallery", model.SiteTitle);
        Assert.Equal("A considered collection.", model.Tagline);
        Assert.Equal(IndexPageState.Populated, model.PageState);
        var album = Assert.Single(model.Albums);
        Assert.Equal("Landscapes", album.Title);
        Assert.Equal(2, album.PhotoCount);
        Assert.Equal("/media/landscapes/first/grid?v=current", album.CoverUrl);
        Assert.Equal(120, album.CoverWidth);
        Assert.Equal(80, album.CoverHeight);
        Assert.DoesNotContain("01-Landscapes/first.jpg", album.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void OnGet_distinguishes_preparing_and_empty_indexes()
    {
        var preparing = new IndexModel(new PortfolioIndex(), Options(), ViewModels());

        preparing.OnGet();

        Assert.Equal(IndexPageState.Preparing, preparing.PageState);
        Assert.Empty(preparing.Albums);

        var empty = new IndexModel(ReadyIndex(PortfolioSnapshot.Empty), Options(), ViewModels());

        empty.OnGet();

        Assert.Equal(IndexPageState.Empty, empty.PageState);
        Assert.Empty(empty.Albums);
    }

    [Fact]
    public void OnGet_keeps_the_last_successful_snapshot_readable_when_degraded()
    {
        var index = ReadyIndex(Snapshot());
        index.MarkDegraded("The source volume is temporarily unavailable.");
        var model = new IndexModel(index, Options(), ViewModels());

        model.OnGet();

        Assert.Equal(IndexPageState.Populated, model.PageState);
        Assert.Single(model.Albums);
    }

    [Fact]
    public async Task Home_page_renders_an_album_card_without_source_paths()
    {
        using var root = new TemporaryDirectory();
        using var app = new FolderFolioWebApplicationFactory(
            Snapshot(),
            new StubDerivativeService { Derivative = new CachedDerivative("unused", 0, DateTimeOffset.UnixEpoch) },
            root.Path);
        using var client = app.CreateClient();

        var response = await client.GetAsync("/", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        Assert.Contains("Landscapes", html, StringComparison.Ordinal);
        Assert.Contains("2 photos", html, StringComparison.Ordinal);
        Assert.Contains("/media/landscapes/first/grid?v=", html, StringComparison.Ordinal);
        Assert.DoesNotContain("01-Landscapes/first.jpg", html, StringComparison.Ordinal);
    }

    private static PortfolioSnapshot Snapshot() => new(ImmutableArray.Create(new IndexedAlbum(
        "01-Landscapes",
        "landscapes",
        "Landscapes",
        "landscapes",
        1,
        ImmutableArray.Create(
            new IndexedPhoto("first", "first.jpg", new SourceFingerprint("01-Landscapes/first.jpg", 1, 1), null, 120, 80),
            new IndexedPhoto("second", "second.jpg", new SourceFingerprint("01-Landscapes/second.jpg", 1, 2), null, 60, 90)))));

    private static FolderFolioOptions Options() => new()
    {
        SiteTitle = "Gallery",
        Tagline = "A considered collection."
    };

    private static PortfolioViewModelFactory ViewModels() => new(new StubMediaUrlBuilder());

    private static PortfolioIndex ReadyIndex(PortfolioSnapshot snapshot)
    {
        var index = new PortfolioIndex();
        index.PublishReady(snapshot, DateTimeOffset.UnixEpoch, TimeSpan.Zero);
        return index;
    }

    private sealed class StubMediaUrlBuilder : IMediaUrlBuilder
    {
        public string Build(IndexedAlbum album, IndexedPhoto photo, FolderFolio.Imaging.DerivativeKind kind) =>
            $"/media/{album.Slug}/{photo.Id}/{kind.ToString().ToLowerInvariant()}?v=current";
    }
}

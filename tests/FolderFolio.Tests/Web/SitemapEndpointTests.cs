using System.Collections.Immutable;
using System.Net;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using FolderFolio.Domain;
using FolderFolio.Imaging;
using FolderFolio.Indexing;
using FolderFolio.Tests.Support;
using FolderFolio.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FolderFolio.Tests.Web;

public sealed class SitemapEndpointTests
{
    private static readonly XNamespace Namespace = "http://www.sitemaps.org/schemas/sitemap/0.9";

    [Theory]
    [InlineData(null, "https://portfolio.example")]
    [InlineData("https://canonical.example/photos/", "https://canonical.example/photos")]
    public async Task Sitemap_lists_absolute_album_and_versioned_photo_urls_and_validates_against_the_official_schema(string? siteUrl, string expectedBase)
    {
        using var root = new TemporaryDirectory();
        var modified = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        var photo = new IndexedPhoto("photo-id", "image.jpg", new SourceFingerprint("01-Album/image.jpg", 3, modified.Ticks), null, 120, 80);
        var album = new IndexedAlbum("01-Album", "album", "Album", "album", 1, ImmutableArray.Create(photo));
        using var app = Factory(new PortfolioSnapshot(ImmutableArray.Create(album)), root.Path);
        using var configured = app.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FolderFolio:SiteUrl"] = siteUrl,
                ["FolderFolio:SiteTitle"] = "My photos & travels"
            })));
        using var client = configured.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://portfolio.example/sitemap.xml");
        request.Headers.Add("X-Forwarded-Proto", "https");
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/xml", response.Content.Headers.ContentType?.MediaType);
        var document = XDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Validate(document);
        var urls = document.Root!.Elements(Namespace + "url").ToArray();
        Assert.Equal(3, urls.Length);
        Assert.Equal(expectedBase + "/albums/album", urls[1].Element(Namespace + "loc")?.Value);
        Assert.Equal("0.8", urls[1].Element(Namespace + "priority")?.Value);
        Assert.NotNull(urls[1].Element(Namespace + "lastmod"));
        Assert.StartsWith(expectedBase + "/media/album/photo-id/web?v=", urls[2].Element(Namespace + "loc")?.Value);
        Assert.Equal("0.5", urls[2].Element(Namespace + "priority")?.Value);
        Assert.Equal("2026-10-01T00:00:00Z", urls[2].Element(Namespace + "lastmod")?.Value);
    }

    [Fact]
    public async Task Request_application_path_is_included_once_in_every_sitemap_url()
    {
        using var root = new TemporaryDirectory();
        var photo = new IndexedPhoto("photo-id", "image.jpg", new SourceFingerprint("Album/image.jpg", 3, DateTimeOffset.UnixEpoch.Ticks), null, 120, 80);
        var album = new IndexedAlbum("Album", "album", "Album", "album", null, ImmutableArray.Create(photo));
        using var app = Factory(new PortfolioSnapshot(ImmutableArray.Create(album)), root.Path);
        using var client = app.CreateClient();
        var context = new DefaultHttpContext { RequestServices = app.Services };
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("portfolio.example");
        context.Request.PathBase = "/photos";
        await using var body = new MemoryStream();
        context.Response.Body = body;
        var result = SitemapEndpoint.Handle(context, app.Services.GetRequiredService<IPortfolioIndex>(),
            app.Services.GetRequiredService<IMediaUrlBuilder>(), app.Services.GetRequiredService<LinkGenerator>(),
            app.Services.GetRequiredService<FolderFolio.Configuration.FolderFolioOptions>());

        await result.ExecuteAsync(context);

        body.Position = 0;
        var xml = await XDocument.LoadAsync(body, LoadOptions.None, TestContext.Current.CancellationToken);
        Assert.All(xml.Descendants(Namespace + "loc"), url => Assert.StartsWith("https://portfolio.example/photos/", url.Value));
        Assert.DoesNotContain("/photos/photos/", xml.ToString());
        Validate(xml);
    }

    [Fact]
    public async Task Sitemap_uses_new_publications_without_retaining_removed_albums()
    {
        using var root = new TemporaryDirectory();
        var oldAlbum = new IndexedAlbum("Old", "old", "Old", "old", null, []);
        using var app = Factory(new PortfolioSnapshot(ImmutableArray.Create(oldAlbum)), root.Path);
        using var client = app.CreateClient();
        Assert.Contains("/albums/old", await client.GetStringAsync("/sitemap.xml", TestContext.Current.CancellationToken));
        var newAlbum = oldAlbum with { DirectoryName = "New", Slug = "new", Title = "New" };
        app.Services.GetRequiredService<IPortfolioIndex>().PublishReady(new PortfolioSnapshot(ImmutableArray.Create(newAlbum)), DateTimeOffset.UtcNow, TimeSpan.Zero);

        var updated = await client.GetStringAsync("/sitemap.xml", TestContext.Current.CancellationToken);

        Assert.Contains("/albums/new", updated);
        Assert.DoesNotContain("/albums/old", updated);
        Validate(XDocument.Parse(updated));
        app.Services.GetRequiredService<IPortfolioIndex>().PublishReady(PortfolioSnapshot.Empty, DateTimeOffset.UtcNow, TimeSpan.Zero);
        Validate(XDocument.Parse(await client.GetStringAsync("/sitemap.xml", TestContext.Current.CancellationToken)));
    }

    private static FolderFolioWebApplicationFactory Factory(PortfolioSnapshot snapshot, string root) =>
        new(snapshot, new StubDerivativeService { Derivative = new CachedDerivative(Path.Combine(root, "unused.webp"), 0, DateTimeOffset.UnixEpoch) }, root);

    private static void Validate(XDocument document)
    {
        var schemas = new XmlSchemaSet();
        using var schema = XmlReader.Create(Path.Combine(AppContext.BaseDirectory, "Web", "sitemap.xsd"));
        schemas.Add(Namespace.NamespaceName, schema);
        document.Validate(schemas, (_, args) => throw args.Exception);
    }
}

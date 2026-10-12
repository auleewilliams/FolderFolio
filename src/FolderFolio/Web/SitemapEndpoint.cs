using System.Xml;
using System.Xml.Linq;
using FolderFolio.Configuration;
using FolderFolio.Imaging;
using FolderFolio.Indexing;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Routing;

namespace FolderFolio.Web;

public static class SitemapEndpoint
{
    public static void Map(WebApplication app) => app.MapGet("/sitemap.xml", Handle);

    public static IResult Handle(HttpContext context, IPortfolioIndex index, IMediaUrlBuilder mediaUrls,
        LinkGenerator links, FolderFolioOptions options)
    {
        var publication = index.Current;
        var request = context.Request;
        var baseUrl = string.IsNullOrWhiteSpace(options.SiteUrl)
            ? UriHelper.BuildAbsolute(request.Scheme, request.Host, request.PathBase).TrimEnd('/')
            : new Uri(options.SiteUrl).AbsoluteUri.TrimEnd('/');
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var root = new XElement(ns + "urlset");
        Add("/", publication.LastSuccessAtUtc, "1.0");
        foreach (var album in publication.Snapshot.Albums)
        {
            var albumPath = links.GetPathByPage("/Albums/Details", values: new { slug = album.Slug })
                ?? throw new InvalidOperationException("Album page route is not available.");
            Add(albumPath, publication.LastSuccessAtUtc, "0.8");
            foreach (var photo in album.Photos)
            {
                Add(mediaUrls.Build(album, photo, DerivativeKind.Web),
                    new DateTimeOffset(photo.Source.LastWriteUtcTicks, TimeSpan.Zero), "0.5");
            }
        }

        context.Response.Headers.CacheControl = "no-store";
        return Results.Text(root.ToString(), "application/xml");

        void Add(string path, DateTimeOffset? lastModified, string priority)
        {
            var url = new XElement(ns + "url", new XElement(ns + "loc", baseUrl + path));
            if (lastModified is { } modified)
            {
                url.Add(new XElement(ns + "lastmod", XmlConvert.ToString(modified.UtcDateTime, XmlDateTimeSerializationMode.Utc)));
            }
            url.Add(new XElement(ns + "priority", priority));
            root.Add(url);
        }
    }
}

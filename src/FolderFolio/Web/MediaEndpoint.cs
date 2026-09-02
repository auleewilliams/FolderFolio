using FolderFolio.Imaging;
using FolderFolio.Indexing;
using Microsoft.Net.Http.Headers;

namespace FolderFolio.Web;

public static class MediaEndpoint
{
    public const string RouteName = "folderfolio-media";

    public static async Task<IResult> HandleAsync(
        string albumSlug,
        string photoId,
        string size,
        string? v,
        HttpContext context,
        IPortfolioIndex index,
        IDerivativeKeyFactory keyFactory,
        IDerivativeService derivativeService,
        IIndexRefreshQueue refreshQueue,
        CancellationToken cancellationToken)
    {
        var snapshot = index.Current.Snapshot;
        if (!snapshot.AlbumsBySlug.TryGetValue(albumSlug, out var album))
        {
            return Results.NotFound();
        }

        var photo = album.Photos.FirstOrDefault(candidate => string.Equals(candidate.Id, photoId, StringComparison.Ordinal));
        if (photo is null || !TryParseKind(size, out var kind))
        {
            return Results.NotFound();
        }

        var identity = keyFactory.Create(photo, kind);
        if (string.IsNullOrEmpty(v) || !string.Equals(v, identity.Version, StringComparison.Ordinal))
        {
            return Results.NotFound();
        }

        try
        {
            var derivative = await derivativeService.GetOrCreateAsync(photo, kind, cancellationToken);
            var headers = context.Response.GetTypedHeaders();
            headers.CacheControl = new CacheControlHeaderValue
            {
                Public = true,
                MaxAge = TimeSpan.FromDays(365),
                Extensions = { new NameValueHeaderValue("immutable") }
            };
            headers.ETag = new EntityTagHeaderValue(identity.ETag);
            headers.LastModified = derivative.LastModifiedUtc;

            return TypedResults.PhysicalFile(
                derivative.AbsolutePath,
                "image/webp",
                lastModified: derivative.LastModifiedUtc,
                entityTag: new EntityTagHeaderValue(identity.ETag));
        }
        catch (StaleSourceException)
        {
            refreshQueue.RequestAlbum(album.DirectoryName);
            return Results.NotFound();
        }
    }

    private static bool TryParseKind(string size, out DerivativeKind kind)
    {
        switch (size)
        {
            case "grid":
                kind = DerivativeKind.Grid;
                return true;
            case "web":
                kind = DerivativeKind.Web;
                return true;
            default:
                kind = default;
                return false;
        }
    }
}

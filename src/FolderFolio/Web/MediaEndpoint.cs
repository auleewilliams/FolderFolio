using FolderFolio.Imaging;
using FolderFolio.Indexing;
using Microsoft.Net.Http.Headers;

namespace FolderFolio.Web;

public static class MediaEndpoint
{
    public const string RouteName = "folderfolio-media";
    public static readonly TimeSpan ImmutableCacheDuration = TimeSpan.FromDays(365);

    public static void Map(WebApplication app) => app.MapGet(
        "/media/{albumSlug}/{photoId}/{size}",
        HandleAsync)
        .WithName(RouteName)
        .RequireRateLimiting(RateLimitingSetup.MediaPolicyName);

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
        ILoggerFactory loggerFactory,
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

        var logger = loggerFactory.CreateLogger("FolderFolio.Web.MediaEndpoint");
        var refreshRequested = false;
        foreach (var candidate in FallbackKinds(kind))
        {
            CachedDerivative derivative;
            try
            {
                derivative = await derivativeService.GetOrCreateAsync(photo, candidate, cancellationToken);
            }
            catch (StaleSourceException)
            {
                if (!refreshRequested) refreshQueue.RequestAlbum(album.DirectoryName);
                return Results.NotFound();
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception,
                    "Derivative {DerivativeKind} failed for {SourcePath} with {ExceptionType}.",
                    candidate, photo.Source.RelativePath, exception.GetType().FullName);
                if (!refreshRequested)
                {
                    refreshQueue.RequestAlbum(album.DirectoryName);
                    refreshRequested = true;
                }
                continue;
            }

            if (candidate != kind)
            {
                context.Response.Headers.CacheControl = "no-store";
                return TypedResults.PhysicalFile(derivative.AbsolutePath, candidate == DerivativeKind.Avif ? "image/avif" : "image/webp");
            }

            var headers = context.Response.GetTypedHeaders();
            headers.CacheControl = new CacheControlHeaderValue
            {
                Public = true,
                MaxAge = ImmutableCacheDuration,
                Extensions = { new NameValueHeaderValue("immutable") }
            };
            headers.ETag = new EntityTagHeaderValue(identity.ETag);
            headers.LastModified = derivative.LastModifiedUtc;

            return TypedResults.PhysicalFile(
                derivative.AbsolutePath,
                kind == DerivativeKind.Avif ? "image/avif" : "image/webp",
                lastModified: derivative.LastModifiedUtc,
                entityTag: new EntityTagHeaderValue(identity.ETag));
        }
        context.Response.Headers.CacheControl = "no-store";
        return Results.Text(
            """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 800 600" role="img" aria-labelledby="title">
              <title id="title">Image unavailable</title>
              <rect width="800" height="600" fill="#242424"/>
              <text x="400" y="300" text-anchor="middle" dominant-baseline="middle" fill="#eee" font-family="sans-serif" font-size="32">Image unavailable</text>
            </svg>
            """, "image/svg+xml");
    }

    private static IEnumerable<DerivativeKind> FallbackKinds(DerivativeKind kind) => kind switch
    {
        DerivativeKind.Grid => [DerivativeKind.Grid, DerivativeKind.Web, DerivativeKind.Full],
        DerivativeKind.Web => [DerivativeKind.Web, DerivativeKind.Full],
        DerivativeKind.Avif => [DerivativeKind.Avif, DerivativeKind.Web, DerivativeKind.Full],
        _ => [kind]
    };

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
            case "full":
                kind = DerivativeKind.Full;
                return true;
            case "avif":
                kind = DerivativeKind.Avif;
                return true;
            default:
                kind = default;
                return false;
        }
    }
}

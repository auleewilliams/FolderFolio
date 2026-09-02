using FolderFolio.Indexing;

namespace FolderFolio.Web;

public static class HealthEndpoint
{
    public static IResult Handle(HttpContext context, IPortfolioIndex index)
    {
        var publication = index.Current;
        context.Response.Headers.CacheControl = "no-store";

        var response = new HealthResponse(
            publication.Status.ToString().ToLowerInvariant(),
            publication.Generation,
            publication.Snapshot.AlbumCount,
            publication.Snapshot.PhotoCount,
            publication.LastSuccessAtUtc);
        var statusCode = publication.Status == Domain.IndexStatus.Ready
            ? StatusCodes.Status200OK
            : StatusCodes.Status503ServiceUnavailable;

        return Results.Json(response, statusCode: statusCode);
    }

    private sealed record HealthResponse(
        string Status,
        long Generation,
        int AlbumCount,
        int PhotoCount,
        DateTimeOffset? LastSuccessAtUtc);
}

using System.Diagnostics;

namespace FolderFolio.Web;

public sealed class MediaLatencyMiddleware(RequestDelegate next, ILogger<MediaLatencyMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/media"))
        {
            await next(context);
            return;
        }

        var started = Stopwatch.GetTimestamp();
        var failed = false;
        try
        {
            await next(context);
        }
        catch
        {
            failed = true;
            throw;
        }
        finally
        {
            var statusCode = failed && context.Response.StatusCode < 400
                ? StatusCodes.Status500InternalServerError
                : context.Response.StatusCode;
            logger.LogInformation(
                "Media request {Method} {Path} returned {StatusCode} in {DurationMilliseconds} ms.",
                context.Request.Method,
                context.Request.Path.Value,
                statusCode,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }
}

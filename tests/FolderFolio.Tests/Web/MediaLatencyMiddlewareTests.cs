using FolderFolio.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Xunit;

namespace FolderFolio.Tests.Web;

public sealed class MediaLatencyMiddlewareTests
{
    [Theory]
    [InlineData(200)]
    [InlineData(404)]
    [InlineData(429)]
    public async Task Media_requests_log_method_path_status_and_numeric_duration(int statusCode)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/media/album/photo/web";
        var logger = new CapturingLogger();
        var middleware = new MediaLatencyMiddleware(async ctx =>
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
            ctx.Response.StatusCode = statusCode;
        }, logger);

        await middleware.InvokeAsync(context);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal("GET", entry["Method"]);
        Assert.Equal("/media/album/photo/web", entry["Path"]);
        Assert.Equal(statusCode, entry["StatusCode"]);
        Assert.True(Assert.IsType<double>(entry["DurationMilliseconds"]) > 0);
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/media-other")]
    public async Task Other_paths_pass_through_without_media_logging(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        var logger = new CapturingLogger();
        var invoked = false;
        var middleware = new MediaLatencyMiddleware(_ =>
        {
            invoked = true;
            return Task.CompletedTask;
        }, logger);

        await middleware.InvokeAsync(context);

        Assert.True(invoked);
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task Unhandled_failures_are_logged_as_server_errors_and_rethrown()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/media/album/photo/grid";
        var logger = new CapturingLogger();
        var failure = new IOException("cache unavailable");
        var middleware = new MediaLatencyMiddleware(_ => throw failure, logger);

        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => middleware.InvokeAsync(context)));

        Assert.Equal(500, Assert.Single(logger.Entries)["StatusCode"]);
    }

    private sealed class CapturingLogger : ILogger<MediaLatencyMiddleware>
    {
        public List<Dictionary<string, object?>> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add(((IEnumerable<KeyValuePair<string, object?>>)state!).ToDictionary());
    }
}

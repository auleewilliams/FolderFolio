using System.Net;
using FolderFolio.Domain;
using FolderFolio.Imaging;
using FolderFolio.Tests.Support;
using Xunit;

namespace FolderFolio.Tests.Web;

public sealed class RobotsEndpointTests
{
    [Fact]
    public async Task Robots_disallows_all_crawlers_as_plain_text()
    {
        using var root = new TemporaryDirectory();
        using var app = new FolderFolioWebApplicationFactory(PortfolioSnapshot.Empty,
            new StubDerivativeService { Derivative = new CachedDerivative(Path.Combine(root.Path, "unused.webp"), 0, DateTimeOffset.UnixEpoch) }, root.Path);
        using var client = app.CreateClient();

        var response = await client.GetAsync("/robots.txt", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("User-agent: *\nDisallow: /\n", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
}

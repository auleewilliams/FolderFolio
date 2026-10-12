using System.Net;
using FolderFolio.Domain;
using FolderFolio.Imaging;
using FolderFolio.Tests.Support;
using FolderFolio.Web;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FolderFolio.Tests.Web;

public sealed class RateLimitingTests
{
    [Theory]
    [InlineData("/media/album/photo/grid?v=missing", 60, HttpStatusCode.NotFound)]
    [InlineData("/health", 10, HttpStatusCode.OK)]
    public async Task Limits_apply_per_forwarded_client_IP(string path, int limit, HttpStatusCode allowedStatus)
    {
        using var root = new TemporaryDirectory();
        using var app = new FolderFolioWebApplicationFactory(PortfolioSnapshot.Empty,
            new StubDerivativeService { Derivative = new CachedDerivative(Path.Combine(root.Path, "unused.webp"), 0, DateTimeOffset.UnixEpoch) }, root.Path);
        using var client = app.CreateClient();
        for (var count = 0; count < limit; count++)
        {
            using var response = await SendAsync("192.0.2.1");
            Assert.Equal(allowedStatus, response.StatusCode);
        }

        using var rejected = await SendAsync("192.0.2.1");
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.True(rejected.Headers.RetryAfter?.Delta > TimeSpan.Zero);

        using var independent = await SendAsync("192.0.2.2");
        Assert.Equal(allowedStatus, independent.StatusCode);

        using var internalRequest = new HttpRequestMessage(HttpMethod.Get, "/health/internal");
        internalRequest.Headers.Add("X-Forwarded-For", "192.0.2.1");
        using var internalResponse = await client.SendAsync(internalRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, internalResponse.StatusCode);
        var routes = app.Services.GetRequiredService<EndpointDataSource>();
        Assert.Contains(routes.Endpoints, endpoint => endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName == HealthEndpoint.InternalRouteName);

        async Task<HttpResponseMessage> SendAsync(string address)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add("X-Forwarded-For", address);
            return await client.SendAsync(request, TestContext.Current.CancellationToken);
        }
    }
}

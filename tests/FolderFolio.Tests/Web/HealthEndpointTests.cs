using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Collections.Immutable;
using FolderFolio.Domain;
using FolderFolio.Indexing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace FolderFolio.Tests.Web;

public sealed class HealthEndpointTests
{
    [Fact]
    public async Task Starting_returns_service_unavailable_without_caching()
    {
        using var app = new HealthWebApplicationFactory(new PortfolioIndex());
        using var client = app.CreateClient();

        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("starting", body.RootElement.GetProperty("status").GetString());
        Assert.Equal(0, body.RootElement.GetProperty("generation").GetInt64());
        Assert.Equal(0, body.RootElement.GetProperty("albumCount").GetInt32());
        Assert.Equal(0, body.RootElement.GetProperty("photoCount").GetInt32());
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("lastSuccessAtUtc").ValueKind);
        Assert.Equal(5, body.RootElement.EnumerateObject().Count());
    }

    [Fact]
    public async Task Ready_returns_public_index_summary()
    {
        var index = new PortfolioIndex();
        var lastSuccessAtUtc = new DateTimeOffset(2026, 9, 3, 1, 2, 3, TimeSpan.Zero);
        index.PublishReady(Snapshot(), lastSuccessAtUtc, TimeSpan.FromSeconds(2));
        using var app = new HealthWebApplicationFactory(index);
        using var client = app.CreateClient();

        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("ready", body.RootElement.GetProperty("status").GetString());
        Assert.Equal(1, body.RootElement.GetProperty("generation").GetInt64());
        Assert.Equal(1, body.RootElement.GetProperty("albumCount").GetInt32());
        Assert.Equal(2, body.RootElement.GetProperty("photoCount").GetInt32());
        Assert.Equal("2026-09-03T01:02:03+00:00", body.RootElement.GetProperty("lastSuccessAtUtc").GetString());
        Assert.Equal(5, body.RootElement.EnumerateObject().Count());
    }

    [Fact]
    public async Task Degraded_does_not_disclose_paths_or_exception_details()
    {
        var index = new PortfolioIndex();
        index.MarkDegraded("C:\\private\\photos: System.InvalidOperationException: scan failed");
        using var app = new HealthWebApplicationFactory(index);
        using var client = app.CreateClient();

        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Contains("\"status\":\"degraded\"", responseText, StringComparison.Ordinal);
        Assert.DoesNotContain("C:\\private\\photos", responseText, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", responseText, StringComparison.Ordinal);
        Assert.DoesNotContain("stack", responseText, StringComparison.OrdinalIgnoreCase);
    }

    private static PortfolioSnapshot Snapshot()
    {
        var photos = ImmutableArray.Create(
            new IndexedPhoto("photo-one", "one.jpg", new SourceFingerprint("01 Album/one.jpg", 1, 1), null, 1, 1),
            new IndexedPhoto("photo-two", "two.jpg", new SourceFingerprint("01 Album/two.jpg", 1, 2), null, 1, 1));
        return new PortfolioSnapshot(ImmutableArray.Create(
            new IndexedAlbum("01 Album", "album", "Album", "album", 1, photos)));
    }

    private sealed class HealthWebApplicationFactory(IPortfolioIndex index) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["FolderFolio:PhotoRoot"] = Path.GetTempPath(),
                    ["FolderFolio:CacheRoot"] = Path.GetTempPath()
                }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPortfolioIndex>();
                services.RemoveAll<IHostedService>();
                services.AddSingleton(index);
            });
        }
    }
}

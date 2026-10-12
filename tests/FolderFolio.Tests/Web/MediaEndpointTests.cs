using System.Collections.Immutable;
using FolderFolio.Domain;
using FolderFolio.Imaging;
using FolderFolio.Indexing;
using FolderFolio.Tests.Support;
using FolderFolio.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FolderFolio.Tests.Web;

public sealed class MediaEndpointTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();
    private readonly TemporaryDirectory _photoRoot = new();

    [Fact]
    public async Task A_current_version_returns_the_webp_with_immutable_cache_metadata()
    {
        var (album, photo, derivative, factory) = CreateFixture();
        using var app = new FolderFolioWebApplicationFactory(new PortfolioSnapshot(ImmutableArray.Create(album)), derivative, _photoRoot.Path);
        using var client = app.CreateClient();
        var version = factory.Create(photo, DerivativeKind.Grid).Version;

        var response = await client.GetAsync($"/media/landscapes/{Uri.EscapeDataString(photo.Id)}/grid?v={version}", TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(_photoRoot.Path, app.Services.GetRequiredService<FolderFolio.Configuration.FolderFolioOptions>().PhotoRoot);
        Assert.Equal("image/webp", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(factory.Create(photo, DerivativeKind.Grid).ETag, response.Headers.ETag?.Tag);
        Assert.Equal("public,max-age=31536000,immutable", response.Headers.CacheControl?.ToString().Replace(" ", string.Empty));
        Assert.Equal(MediaEndpoint.ImmutableCacheDuration, response.Headers.CacheControl?.MaxAge);
        await AssertDoesNotDisclosePhotoRoot(response);
    }

    [Theory]
    [InlineData("unknown", "photo-id", "grid", "version")]
    [InlineData("landscapes", "unknown", "grid", "version")]
    [InlineData("landscapes", "photo-id", "thumbnail", "version")]
    [InlineData("landscapes", "photo-id", "grid", "")]
    [InlineData("landscapes", "photo-id", "grid", "stale")]
    public async Task An_unresolvable_media_lookup_returns_not_found(string albumSlug, string photoId, string size, string version)
    {
        var (album, _, derivative, _) = CreateFixture();
        using var app = new FolderFolioWebApplicationFactory(new PortfolioSnapshot(ImmutableArray.Create(album)), derivative, _photoRoot.Path);
        using var client = app.CreateClient();
        var query = version.Length == 0 ? string.Empty : $"?v={version}";

        var response = await client.GetAsync($"/media/{albumSlug}/{photoId}/{size}{query}", TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
        await AssertDoesNotDisclosePhotoRoot(response);
    }

    [Fact]
    public async Task A_stale_source_queues_its_indexed_album_and_returns_not_found()
    {
        var (album, photo, derivative, factory) = CreateFixture();
        derivative.ThrowStaleSource = true;
        using var app = new FolderFolioWebApplicationFactory(new PortfolioSnapshot(ImmutableArray.Create(album)), derivative, _photoRoot.Path);
        using var client = app.CreateClient();

        var response = await client.GetAsync($"/media/landscapes/{photo.Id}/grid?v={factory.Create(photo, DerivativeKind.Grid).Version}", TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
        await AssertDoesNotDisclosePhotoRoot(response);
        Assert.True(app.RefreshQueue.TryRead(out var request));
        Assert.False(request.FullScan);
        Assert.Equal([album.DirectoryName], request.AlbumDirectoryNames);
    }

    [Fact]
    public async Task A_source_removed_between_service_validation_and_generator_resolution_returns_not_found_and_queues_refresh()
    {
        var albumDirectory = Directory.CreateDirectory(Path.Combine(_photoRoot.Path, "01 Landscapes"));
        var sourcePath = Path.Combine(albumDirectory.FullName, "coast.jpg");
        ImageFixtureFactory.CreateJpeg(sourcePath);
        var source = new FileInfo(sourcePath);
        var photo = new IndexedPhoto(
            "photo-id",
            source.Name,
            new SourceFingerprint("01 Landscapes/coast.jpg", source.Length, source.LastWriteTimeUtc.Ticks),
            null,
            120,
            80);
        var album = new IndexedAlbum(
            "01 Landscapes",
            "landscapes",
            "Landscapes",
            "landscapes",
            1,
            ImmutableArray.Create(photo));
        var options = new FolderFolio.Configuration.FolderFolioOptions
        {
            PhotoRoot = _photoRoot.Path,
            CacheRoot = _directory.Path
        };
        var keyFactory = new DerivativeKeyFactory(options);
        var serviceGuard = new SourcePathGuard(options);
        var generatorGuard = new RemoveSourceBeforeResolutionGuard(new SourcePathGuard(options), sourcePath);
        var derivativeService = new DerivativeService(
            keyFactory,
            serviceGuard,
            new ImageSharpDerivativeGenerator(generatorGuard),
            options,
            new TestHostApplicationLifetime());
        using var app = new FolderFolioWebApplicationFactory(
            new PortfolioSnapshot(ImmutableArray.Create(album)),
            derivativeService,
            _photoRoot.Path);
        using var client = app.CreateClient();

        var response = await client.GetAsync(
            $"/media/landscapes/{photo.Id}/grid?v={keyFactory.Create(photo, DerivativeKind.Grid).Version}",
            TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
        Assert.True(app.RefreshQueue.TryRead(out var request));
        Assert.False(request.FullScan);
        Assert.Equal([album.DirectoryName], request.AlbumDirectoryNames);
    }

    [Fact]
    public async Task Exhausted_derivative_failures_return_an_uncached_svg_and_queue_refresh()
    {
        var (album, photo, derivative, factory) = CreateFixture();
        derivative.Failure = new IOException("cache unavailable");
        using var app = new FolderFolioWebApplicationFactory(
            new PortfolioSnapshot(ImmutableArray.Create(album)),
            derivative,
            _photoRoot.Path);
        using var client = app.CreateClient();

        var response = await client.GetAsync(
            $"/media/landscapes/{photo.Id}/grid?v={factory.Create(photo, DerivativeKind.Grid).Version}",
            TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/svg+xml", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Null(response.Headers.ETag);
        var svg = System.Xml.Linq.XDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("svg", svg.Root?.Name.LocalName);
        Assert.Contains("Image unavailable", svg.ToString());
        await AssertDoesNotDisclosePhotoRoot(response);
        Assert.Equal([DerivativeKind.Grid, DerivativeKind.Web, DerivativeKind.Full], derivative.RequestedKinds);
        Assert.True(app.RefreshQueue.TryRead(out var request));
        Assert.Equal([album.DirectoryName], request.AlbumDirectoryNames);
        Assert.False(app.RefreshQueue.TryRead(out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_failed_grid_derivative_falls_back_to_web_then_full_without_immutable_caching(bool webFails)
    {
        var (album, photo, derivative, factory) = CreateFixture();
        derivative.Failures[DerivativeKind.Grid] = new IOException("grid failed");
        if (webFails) derivative.Failures[DerivativeKind.Web] = new IOException("web failed");
        using var app = new FolderFolioWebApplicationFactory(new PortfolioSnapshot(ImmutableArray.Create(album)), derivative, _photoRoot.Path);
        using var client = app.CreateClient();

        var response = await client.GetAsync($"/media/landscapes/{photo.Id}/grid?v={factory.Create(photo, DerivativeKind.Grid).Version}", TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/webp", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Null(response.Headers.ETag);
        Assert.Equal(webFails ? [DerivativeKind.Grid, DerivativeKind.Web, DerivativeKind.Full] : new[] { DerivativeKind.Grid, DerivativeKind.Web }, derivative.RequestedKinds);
        Assert.True(app.RefreshQueue.TryRead(out _));
    }

    public void Dispose()
    {
        _photoRoot.Dispose();
        _directory.Dispose();
    }

    [Fact]
    public async Task A_failed_web_derivative_falls_back_directly_to_full()
    {
        var (album, photo, derivative, factory) = CreateFixture();
        derivative.Failures[DerivativeKind.Web] = new IOException("web failed");
        using var app = new FolderFolioWebApplicationFactory(new PortfolioSnapshot(ImmutableArray.Create(album)), derivative, _photoRoot.Path);
        using var client = app.CreateClient();

        var response = await client.GetAsync($"/media/landscapes/{photo.Id}/web?v={factory.Create(photo, DerivativeKind.Web).Version}", TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([DerivativeKind.Web, DerivativeKind.Full], derivative.RequestedKinds);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Failures_log_each_exception_type_and_source_path()
    {
        var (album, photo, derivative, factory) = CreateFixture();
        derivative.Failure = new IOException("cache unavailable");
        using var logs = new FailureLogProvider();
        using var app = new FolderFolioWebApplicationFactory(new PortfolioSnapshot(ImmutableArray.Create(album)), derivative, _photoRoot.Path);
        using var configured = app.WithWebHostBuilder(builder => builder.ConfigureLogging(logging => logging.AddProvider(logs)));
        using var client = configured.CreateClient();

        await client.GetAsync($"/media/landscapes/{photo.Id}/grid?v={factory.Create(photo, DerivativeKind.Grid).Version}", TestContext.Current.CancellationToken);

        Assert.Equal(3, logs.Entries.Count);
        Assert.All(logs.Entries, entry =>
        {
            Assert.IsType<IOException>(entry.Exception);
            Assert.Equal(typeof(IOException).FullName, entry.Properties["ExceptionType"]);
            Assert.Equal(photo.Source.RelativePath, entry.Properties["SourcePath"]);
        });
    }

    [Fact]
    public async Task Cancellation_is_propagated_without_fallback_or_refresh()
    {
        var (album, photo, derivative, factory) = CreateFixture();
        derivative.Failure = new OperationCanceledException();
        var index = new PortfolioIndex();
        index.PublishReady(new PortfolioSnapshot(ImmutableArray.Create(album)), DateTimeOffset.UnixEpoch, TimeSpan.Zero);
        var queue = new IndexRefreshQueue();

        await Assert.ThrowsAsync<OperationCanceledException>(() => MediaEndpoint.HandleAsync(
            "landscapes", photo.Id, "grid", factory.Create(photo, DerivativeKind.Grid).Version,
            new DefaultHttpContext(), index, factory, derivative, queue, NullLoggerFactory.Instance,
            TestContext.Current.CancellationToken));

        Assert.Equal([DerivativeKind.Grid], derivative.RequestedKinds);
        Assert.False(queue.TryRead(out _));
    }

    private sealed class FailureLogProvider : ILoggerProvider, ILogger
    {
        public List<(Exception? Exception, Dictionary<string, object?> Properties)> Entries { get; } = [];
        public ILogger CreateLogger(string categoryName) => categoryName == "FolderFolio.Web.MediaEndpoint" ? this : NullLogger.Instance;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((exception, ((IEnumerable<KeyValuePair<string, object?>>)state!).ToDictionary()));
        public void Dispose() { }
    }

    private async Task AssertDoesNotDisclosePhotoRoot(HttpResponseMessage response)
    {
        var responseText = $"{response}\n{await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)}";

        Assert.DoesNotContain(
            _photoRoot.Path,
            responseText,
            StringComparison.Ordinal);
    }

    private (IndexedAlbum Album, IndexedPhoto Photo, StubDerivativeService Derivative, DerivativeKeyFactory KeyFactory) CreateFixture()
    {
        var derivativePath = Path.Combine(_directory.Path, "cached.webp");
        File.WriteAllBytes(derivativePath, [1, 2, 3]);
        var lastModified = new DateTimeOffset(2026, 9, 3, 0, 0, 0, TimeSpan.Zero);
        File.SetLastWriteTimeUtc(derivativePath, lastModified.UtcDateTime);
        var photo = new IndexedPhoto("photo-id", "coast.jpg", new SourceFingerprint("01 Landscapes/coast.jpg", 3, 123), null, 120, 80);
        var album = new IndexedAlbum("01 Landscapes", "landscapes", "Landscapes", "landscapes", 1, ImmutableArray.Create(photo));
        return (album, photo, new StubDerivativeService { Derivative = new CachedDerivative(derivativePath, 3, lastModified) }, new DerivativeKeyFactory(new FolderFolio.Configuration.FolderFolioOptions()));
    }

    private sealed class RemoveSourceBeforeResolutionGuard(ISourcePathGuard inner, string sourcePath) : ISourcePathGuard
    {
        private int sourceRemoved;

        public bool TryResolve(IndexedPhoto photo, out string resolvedPath)
        {
            if (Interlocked.Exchange(ref sourceRemoved, 1) == 0)
            {
                File.Delete(sourcePath);
            }

            return inner.TryResolve(photo, out resolvedPath);
        }
    }
}

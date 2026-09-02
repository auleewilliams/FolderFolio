using FolderFolio.Configuration;
using FolderFolio.Domain;
using FolderFolio.Imaging;
using FolderFolio.Tests.Support;
using Xunit;

namespace FolderFolio.Tests.Imaging;

public sealed class DerivativeServiceTests
{
    [Fact]
    public async Task GetOrCreateAsync_coalesces_concurrent_requests_and_publishes_only_completed_output()
    {
        using var directory = new TemporaryDirectory();
        var photo = CreatePhoto(directory);
        var generator = new CountingDerivativeGenerator();
        var service = CreateService(directory, generator);
        var finalPath = ExpectedPath(directory, photo);

        var requests = Enumerable.Range(0, 20)
            .Select(_ => service.GetOrCreateAsync(photo, DerivativeKind.Grid, TestContext.Current.CancellationToken))
            .ToArray();

        await WaitForCountAsync(generator, 1);
        Assert.False(File.Exists(finalPath));

        generator.Release();
        var derivatives = await Task.WhenAll(requests);

        Assert.All(derivatives, derivative => Assert.Equal(finalPath, derivative.AbsolutePath));
        Assert.Equal(1, generator.Count);
        Assert.True(File.Exists(finalPath));
        Assert.Empty(Directory.EnumerateFiles(directory.Path, "*.tmp", SearchOption.AllDirectories));

        var cached = await service.GetOrCreateAsync(photo, DerivativeKind.Grid, TestContext.Current.CancellationToken);
        Assert.Equal(finalPath, cached.AbsolutePath);
        Assert.Equal(1, generator.Count);
    }

    [Fact]
    public async Task GetOrCreateAsync_removes_temporary_output_when_generation_fails()
    {
        using var directory = new TemporaryDirectory();
        var photo = CreatePhoto(directory);
        var generator = new CountingDerivativeGenerator(new InvalidOperationException("encoding failed"));
        var service = CreateService(directory, generator);
        var finalPath = ExpectedPath(directory, photo);

        var generation = service.GetOrCreateAsync(photo, DerivativeKind.Grid, TestContext.Current.CancellationToken);
        await WaitForCountAsync(generator, 1);
        generator.Release();

        await Assert.ThrowsAsync<InvalidOperationException>(() => generation);
        Assert.False(File.Exists(finalPath));
        Assert.Empty(Directory.EnumerateFiles(directory.Path, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task GetOrCreateAsync_rejects_a_changed_source_before_invoking_the_generator()
    {
        using var directory = new TemporaryDirectory();
        var photo = CreatePhoto(directory);
        File.AppendAllText(Path.Combine(directory.Path, "photos", photo.Source.RelativePath), "changed");
        var generator = new CountingDerivativeGenerator();
        var service = CreateService(directory, generator);

        await Assert.ThrowsAsync<StaleSourceException>(() =>
            service.GetOrCreateAsync(photo, DerivativeKind.Grid, TestContext.Current.CancellationToken));

        Assert.Equal(0, generator.Count);
    }

    private static DerivativeService CreateService(TemporaryDirectory directory, CountingDerivativeGenerator generator) =>
        new(
            new DerivativeKeyFactory(new FolderFolioOptions { PhotoRoot = Path.Combine(directory.Path, "photos"), CacheRoot = directory.Path }),
            new SourcePathGuard(new FolderFolioOptions { PhotoRoot = Path.Combine(directory.Path, "photos") }),
            generator,
            new FolderFolioOptions { CacheRoot = directory.Path },
            new TestHostApplicationLifetime());

    private static IndexedPhoto CreatePhoto(TemporaryDirectory directory)
    {
        var root = directory.CreateDirectory("photos");
        var sourcePath = Path.Combine(root, "photo.jpg");
        File.WriteAllBytes(sourcePath, [5, 6, 7]);
        var info = new FileInfo(sourcePath);
        return new IndexedPhoto("photo-id", "photo.jpg", new SourceFingerprint("photo.jpg", info.Length, info.LastWriteTimeUtc.Ticks), null, 120, 80);
    }

    private static string ExpectedPath(TemporaryDirectory directory, IndexedPhoto photo)
    {
        var identity = new DerivativeKeyFactory(new FolderFolioOptions { GridLongEdge = 400, WebLongEdge = 2000, WebPQuality = 82 }).Create(photo, DerivativeKind.Grid);
        return Path.Combine(directory.Path, identity.CacheKey[..2], $"{identity.CacheKey}.webp");
    }

    private static async Task WaitForCountAsync(CountingDerivativeGenerator generator, int expected)
    {
        while (generator.Count < expected)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }
}

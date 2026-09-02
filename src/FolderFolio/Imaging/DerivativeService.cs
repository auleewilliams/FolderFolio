using System.Collections.Concurrent;
using FolderFolio.Configuration;
using FolderFolio.Domain;
using Microsoft.Extensions.Hosting;

namespace FolderFolio.Imaging;

public sealed class DerivativeService : IDerivativeService
{
    private readonly IDerivativeKeyFactory _keyFactory;
    private readonly ISourcePathGuard _sourcePathGuard;
    private readonly IImageDerivativeGenerator _generator;
    private readonly string _cacheRoot;
    private readonly CancellationToken _applicationStopping;
    private readonly ConcurrentDictionary<string, Lazy<Task<CachedDerivative>>> _inFlight = new(StringComparer.Ordinal);

    public DerivativeService(
        IDerivativeKeyFactory keyFactory,
        ISourcePathGuard sourcePathGuard,
        IImageDerivativeGenerator generator,
        FolderFolioOptions options,
        IHostApplicationLifetime applicationLifetime)
    {
        ArgumentNullException.ThrowIfNull(keyFactory);
        ArgumentNullException.ThrowIfNull(sourcePathGuard);
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(applicationLifetime);

        _keyFactory = keyFactory;
        _sourcePathGuard = sourcePathGuard;
        _generator = generator;
        _cacheRoot = options.CacheRoot;
        _applicationStopping = applicationLifetime.ApplicationStopping;
    }

    public async Task<CachedDerivative> GetOrCreateAsync(
        IndexedPhoto photo,
        DerivativeKind kind,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(photo);

        EnsureSourceIsCurrent(photo);
        var identity = _keyFactory.Create(photo, kind);
        var finalPath = Path.Combine(_cacheRoot, identity.CacheKey[..2], $"{identity.CacheKey}.webp");
        if (TryGetCached(finalPath, out var cached))
        {
            return cached;
        }

        var candidate = new Lazy<Task<CachedDerivative>>(
            () => GenerateAsync(photo, identity, finalPath),
            LazyThreadSafetyMode.ExecutionAndPublication);
        var selected = _inFlight.GetOrAdd(identity.CacheKey, candidate);
        var sharedGeneration = selected.Value;
        _ = sharedGeneration.ContinueWith(
            _ => RemoveExact(identity.CacheKey, selected),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        return await sharedGeneration.WaitAsync(cancellationToken);
    }

    private async Task<CachedDerivative> GenerateAsync(IndexedPhoto photo, DerivativeIdentity identity, string finalPath)
    {
        EnsureSourceIsCurrent(photo);
        if (TryGetCached(finalPath, out var cached))
        {
            return cached;
        }

        var destinationDirectory = Path.GetDirectoryName(finalPath)!;
        Directory.CreateDirectory(destinationDirectory);
        var temporaryPath = Path.Combine(destinationDirectory, $"{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var destination = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await _generator.WriteWebPAsync(
                    photo,
                    destination,
                    identity.MaxLongEdge,
                    identity.WebPQuality,
                    _applicationStopping);
                await destination.FlushAsync(_applicationStopping);
            }

            EnsureSourceIsCurrent(photo);
            File.Move(temporaryPath, finalPath, overwrite: false);
            return ReadCached(finalPath);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private void EnsureSourceIsCurrent(IndexedPhoto photo)
    {
        if (!_sourcePathGuard.TryResolve(photo, out _))
        {
            throw new StaleSourceException();
        }
    }

    private static bool TryGetCached(string path, out CachedDerivative cached)
    {
        if (!File.Exists(path))
        {
            cached = null!;
            return false;
        }

        cached = ReadCached(path);
        return true;
    }

    private static CachedDerivative ReadCached(string path)
    {
        var info = new FileInfo(path);
        return new CachedDerivative(info.FullName, info.Length, info.LastWriteTimeUtc);
    }

    private void RemoveExact(string key, Lazy<Task<CachedDerivative>> lazy)
    {
        ((ICollection<KeyValuePair<string, Lazy<Task<CachedDerivative>>>>)_inFlight)
            .Remove(new KeyValuePair<string, Lazy<Task<CachedDerivative>>>(key, lazy));
    }
}

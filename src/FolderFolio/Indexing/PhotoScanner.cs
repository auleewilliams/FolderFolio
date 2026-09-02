using System.Collections.Immutable;
using FolderFolio.Configuration;
using FolderFolio.Domain;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;

namespace FolderFolio.Indexing;

public sealed class PhotoScanner : IPhotoScanner
{
    private const long MaxDecodedPixelBytes = 512L * 1024 * 1024;
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bmp", ".gif", ".jpeg", ".jpg", ".png", ".tif", ".tiff", ".webp"
    };

    private readonly string photoRoot;
    private readonly IImageMetadataReader metadataReader;
    private readonly ILogger<PhotoScanner>? logger;

    public PhotoScanner(
        FolderFolioOptions options,
        IImageMetadataReader metadataReader,
        ILogger<PhotoScanner>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(metadataReader);

        photoRoot = Path.GetFullPath(options.PhotoRoot);
        this.metadataReader = metadataReader;
        this.logger = logger;
    }

    public async Task<PhotoScanResult> ScanAllAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!Directory.Exists(photoRoot))
        {
            return new PhotoScanResult(PortfolioSnapshot.Empty, 0);
        }

        var scannedAlbums = new List<ScannedAlbum>();
        var skippedFileCount = 0;

        foreach (var directoryPath in Directory.EnumerateDirectories(photoRoot, "*", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsReparsePoint(directoryPath))
            {
                continue;
            }

            var (album, skipped) = await ScanAlbumAsync(Path.GetFileName(directoryPath), cancellationToken);
            scannedAlbums.Add(album);
            skippedFileCount += skipped;
        }

        return new PhotoScanResult(new PortfolioSnapshot(AlbumCatalogBuilder.Build(scannedAlbums)), skippedFileCount);
    }

    public async Task<PhotoScanResult> RescanAlbumsAsync(
        PortfolioSnapshot current,
        IReadOnlySet<string> albumDirectoryNames,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(albumDirectoryNames);
        cancellationToken.ThrowIfCancellationRequested();

        var targets = new HashSet<string>(StringComparer.Ordinal);
        foreach (var directoryName in albumDirectoryNames)
        {
            ValidateAlbumDirectoryName(directoryName);
            targets.Add(directoryName);
        }

        var scannedAlbums = current.Albums
            .Where(album => !targets.Contains(album.DirectoryName))
            .Select(ToScannedAlbum)
            .ToList();
        var skippedFileCount = 0;

        foreach (var directoryName in targets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directoryPath = Path.Combine(photoRoot, directoryName);
            if (!Directory.Exists(directoryPath) || IsReparsePoint(directoryPath))
            {
                continue;
            }

            var (album, skipped) = await ScanAlbumAsync(directoryName, cancellationToken);
            scannedAlbums.Add(album);
            skippedFileCount += skipped;
        }

        return new PhotoScanResult(new PortfolioSnapshot(AlbumCatalogBuilder.Build(scannedAlbums)), skippedFileCount);
    }

    private async Task<(ScannedAlbum Album, int SkippedFileCount)> ScanAlbumAsync(
        string directoryName,
        CancellationToken cancellationToken)
    {
        var albumName = AlbumNameParser.Parse(directoryName);
        var directoryPath = Path.Combine(photoRoot, directoryName);
        var photos = new List<IndexedPhoto>();
        var skippedFileCount = 0;

        foreach (var nestedDirectory in Directory.EnumerateDirectories(directoryPath, "*", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            skippedFileCount++;
        }

        foreach (var sourcePath in Directory.EnumerateFiles(directoryPath, "*", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (!SupportedExtensions.Contains(Path.GetExtension(sourcePath)) || IsReparsePoint(sourcePath))
                {
                    skippedFileCount++;
                    continue;
                }

                var before = ReadFingerprint(sourcePath, directoryName);
                var metadata = await metadataReader.IdentifyAsync(sourcePath, cancellationToken);
                if (metadata.EstimatedPixelBytes > MaxDecodedPixelBytes)
                {
                    skippedFileCount++;
                    LogSkipped(before.RelativePath, "decoded pixel size exceeds the limit");
                    continue;
                }

                var after = ReadFingerprint(sourcePath, directoryName);
                if (before != after)
                {
                    skippedFileCount++;
                    LogSkipped(before.RelativePath, "source changed while metadata was read");
                    continue;
                }

                photos.Add(new IndexedPhoto(
                    PhotoIdentity.FromRelativePath(before.RelativePath),
                    Path.GetFileName(sourcePath),
                    before,
                    metadata.CapturedAt,
                    metadata.Width,
                    metadata.Height));
            }
            catch (IOException)
            {
                skippedFileCount++;
                LogSkipped(RelativePath(directoryName, Path.GetFileName(sourcePath)), "source could not be read");
            }
            catch (UnauthorizedAccessException)
            {
                skippedFileCount++;
                LogSkipped(RelativePath(directoryName, Path.GetFileName(sourcePath)), "source could not be read");
            }
            catch (ImageFormatException)
            {
                skippedFileCount++;
                LogSkipped(RelativePath(directoryName, Path.GetFileName(sourcePath)), "image metadata could not be read");
            }
        }

        var orderedPhotos = photos
            .OrderBy(photo => photo.CapturedAt.HasValue ? 0 : 1)
            .ThenBy(photo => photo.CapturedAt)
            .ThenBy(photo => photo.FileName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(photo => photo.FileName, StringComparer.Ordinal)
            .ToImmutableArray();
        return (new ScannedAlbum(
            directoryName,
            albumName.Title,
            albumName.BaseSlug,
            albumName.SortPrefix,
            orderedPhotos),
            skippedFileCount);
    }

    private static ScannedAlbum ToScannedAlbum(IndexedAlbum album) =>
        new(album.DirectoryName, album.Title, album.BaseSlug, album.SortPrefix, album.Photos);

    private static bool IsReparsePoint(string path) =>
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    private static SourceFingerprint ReadFingerprint(string sourcePath, string directoryName)
    {
        var file = new FileInfo(sourcePath);
        return new SourceFingerprint(
            RelativePath(directoryName, file.Name),
            file.Length,
            file.LastWriteTimeUtc.Ticks);
    }

    private static string RelativePath(string directoryName, string fileName) =>
        $"{directoryName}/{fileName}".Replace('\\', '/');

    private static void ValidateAlbumDirectoryName(string directoryName)
    {
        if (string.IsNullOrWhiteSpace(directoryName) ||
            Path.IsPathRooted(directoryName) ||
            !string.Equals(Path.GetFileName(directoryName), directoryName, StringComparison.Ordinal) ||
            directoryName is "." or ".." ||
            directoryName.Contains('/') ||
            directoryName.Contains('\\'))
        {
            throw new ArgumentException("Album directory names must be a single relative directory name.", nameof(directoryName));
        }
    }

    private void LogSkipped(string relativePath, string reason) =>
        logger?.LogWarning("Skipped source photo {RelativePath}: {Reason}", relativePath, reason);
}

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
        ".jpeg", ".jpg", ".png", ".webp"
    };

    private readonly string photoRoot;
    private readonly IImageMetadataReader metadataReader;
    private readonly ILogger<PhotoScanner>? logger;
    private readonly IPhotoScanFileSystem fileSystem;

    public PhotoScanner(
        FolderFolioOptions options,
        IImageMetadataReader metadataReader,
        ILogger<PhotoScanner>? logger = null,
        IPhotoScanFileSystem? fileSystem = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(metadataReader);

        photoRoot = Path.GetFullPath(options.PhotoRoot);
        this.metadataReader = metadataReader;
        this.logger = logger;
        this.fileSystem = fileSystem ?? new PhotoScanFileSystem();
    }

    public async Task<PhotoScanResult> ScanAllAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!fileSystem.DirectoryExists(photoRoot))
        {
            throw new DirectoryNotFoundException("The photo root is unavailable.");
        }

        var scannedAlbums = new List<ScannedAlbum>();
        var skippedFileCount = 0;

        foreach (var directoryPath in fileSystem.EnumerateDirectories(photoRoot))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directoryName = Path.GetFileName(directoryPath);

            if (directoryName.Contains('\\'))
            {
                skippedFileCount++;
                LogSkippedAlbum(directoryName, "album directory contains an unsupported path character");
                continue;
            }

            var result = await TryScanAlbumAsync(directoryName, cancellationToken);
            if (result is { } scanned)
            {
                scannedAlbums.Add(scanned.Album);
                skippedFileCount += scanned.SkippedFileCount;
            }
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
            var result = await TryScanAlbumAsync(directoryName, cancellationToken);
            if (result is { } scanned)
            {
                scannedAlbums.Add(scanned.Album);
                skippedFileCount += scanned.SkippedFileCount;
            }
        }

        return new PhotoScanResult(new PortfolioSnapshot(AlbumCatalogBuilder.Build(scannedAlbums)), skippedFileCount);
    }

    private async Task<(ScannedAlbum Album, int SkippedFileCount)?> TryScanAlbumAsync(
        string directoryName,
        CancellationToken cancellationToken)
    {
        try
        {
            var directoryPath = Path.Combine(photoRoot, directoryName);
            if (!fileSystem.DirectoryExists(directoryPath) || IsReparsePoint(directoryPath))
            {
                return null;
            }

            return await ScanAlbumAsync(directoryName, cancellationToken);
        }
        catch (IOException)
        {
            LogSkippedAlbum(directoryName, "album directory could not be read");
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            LogSkippedAlbum(directoryName, "album directory could not be read");
            return null;
        }
    }

    private async Task<(ScannedAlbum Album, int SkippedFileCount)> ScanAlbumAsync(
        string directoryName,
        CancellationToken cancellationToken)
    {
        var albumName = AlbumNameParser.Parse(directoryName);
        var directoryPath = Path.Combine(photoRoot, directoryName);
        var photos = new List<IndexedPhoto>();
        var skippedFileCount = 0;

        foreach (var nestedDirectory in fileSystem.EnumerateDirectories(directoryPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            skippedFileCount++;
        }

        foreach (var sourcePath in fileSystem.EnumerateFiles(directoryPath))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var fileName = Path.GetFileName(sourcePath);
                if (fileName.Contains('\\') ||
                    !SupportedExtensions.Contains(Path.GetExtension(sourcePath)) ||
                    IsReparsePoint(sourcePath))
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

    private bool IsReparsePoint(string path) =>
        (fileSystem.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    private static SourceFingerprint ReadFingerprint(string sourcePath, string directoryName)
    {
        var file = new FileInfo(sourcePath);
        return new SourceFingerprint(
            RelativePath(directoryName, file.Name),
            file.Length,
            file.LastWriteTimeUtc.Ticks);
    }

    private static string RelativePath(string directoryName, string fileName) =>
        $"{directoryName}/{fileName}";

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

    private void LogSkippedAlbum(string directoryName, string reason) =>
        logger?.LogWarning("Skipped album {AlbumDirectoryName}: {Reason}", directoryName, reason);
}

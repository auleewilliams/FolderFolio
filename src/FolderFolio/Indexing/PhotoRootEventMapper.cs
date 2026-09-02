using System.Collections.Immutable;

namespace FolderFolio.Indexing;

public sealed class PhotoRootEventMapper
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    private readonly string photoRoot;

    public PhotoRootEventMapper(string photoRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(photoRoot);
        this.photoRoot = Path.GetFullPath(photoRoot);
    }

    public IndexRefreshRequest? MapPath(string path)
    {
        var albumDirectoryName = AlbumDirectoryNameFor(path, out var requiresFullScan);
        return requiresFullScan
            ? IndexRefreshRequest.Full
            : albumDirectoryName is null
                ? null
                : IndexRefreshRequest.Album(albumDirectoryName);
    }

    public IndexRefreshRequest? MapRename(string oldPath, string newPath)
    {
        var oldAlbumDirectoryName = AlbumDirectoryNameFor(oldPath, out var oldRequiresFullScan);
        var newAlbumDirectoryName = AlbumDirectoryNameFor(newPath, out var newRequiresFullScan);
        if (oldRequiresFullScan || newRequiresFullScan)
        {
            return IndexRefreshRequest.Full;
        }

        var albums = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
        if (oldAlbumDirectoryName is not null)
        {
            albums.Add(oldAlbumDirectoryName);
        }

        if (newAlbumDirectoryName is not null)
        {
            albums.Add(newAlbumDirectoryName);
        }

        return albums.Count == 0 ? null : new IndexRefreshRequest(false, albums.ToImmutable());
    }

    private string? AlbumDirectoryNameFor(string path, out bool requiresFullScan)
    {
        requiresFullScan = false;
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        string canonicalPath;
        try
        {
            canonicalPath = Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }

        var relativePath = Path.GetRelativePath(photoRoot, canonicalPath);
        if (IsOutsidePhotoRoot(relativePath))
        {
            return null;
        }

        if (relativePath == ".")
        {
            requiresFullScan = true;
            return null;
        }

        var segments = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (segments.Length == 1)
        {
            requiresFullScan = true;
            return null;
        }

        return segments.Length == 2 && SupportedExtensions.Contains(Path.GetExtension(segments[1]))
            ? segments[0]
            : null;
    }

    private static bool IsOutsidePhotoRoot(string relativePath) =>
        relativePath == ".." ||
        relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
        relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
}

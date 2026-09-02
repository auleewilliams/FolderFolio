using FolderFolio.Configuration;
using FolderFolio.Domain;

namespace FolderFolio.Imaging;

public sealed class SourcePathGuard : ISourcePathGuard
{
    private readonly string _canonicalPhotoRoot;
    private readonly StringComparison _pathComparison;

    public SourcePathGuard(FolderFolioOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var canonicalPhotoRoot = Path.GetFullPath(options.PhotoRoot);
        _canonicalPhotoRoot = Path.EndsInDirectorySeparator(canonicalPhotoRoot)
            ? canonicalPhotoRoot
            : canonicalPhotoRoot + Path.DirectorySeparatorChar;
        _pathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    }

    public bool TryResolve(IndexedPhoto photo, out string sourcePath)
    {
        ArgumentNullException.ThrowIfNull(photo);
        sourcePath = string.Empty;

        if (Path.IsPathRooted(photo.Source.RelativePath))
        {
            return false;
        }

        string candidate;
        try
        {
            candidate = Path.GetFullPath(Path.Combine(_canonicalPhotoRoot, photo.Source.RelativePath));
        }
        catch (ArgumentException)
        {
            return false;
        }

        if (!candidate.StartsWith(_canonicalPhotoRoot, _pathComparison))
        {
            return false;
        }

        try
        {
            var info = new FileInfo(candidate);
            if (!info.Exists || (info.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                return false;
            }

            if (info.Length != photo.Source.Length || info.LastWriteTimeUtc.Ticks != photo.Source.LastWriteUtcTicks)
            {
                return false;
            }
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }

        sourcePath = candidate;
        return true;
    }
}

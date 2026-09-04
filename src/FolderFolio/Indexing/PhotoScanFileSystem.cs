namespace FolderFolio.Indexing;

public sealed class PhotoScanFileSystem : IPhotoScanFileSystem
{
    public bool DirectoryExists(string path) => Directory.Exists(path);

    public IEnumerable<string> EnumerateDirectories(string path) =>
        Directory.EnumerateDirectories(path, "*", SearchOption.TopDirectoryOnly);

    public IEnumerable<string> EnumerateFiles(string path) =>
        Directory.EnumerateFiles(path, "*", SearchOption.TopDirectoryOnly);

    public FileAttributes GetAttributes(string path) => File.GetAttributes(path);
}

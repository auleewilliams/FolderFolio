namespace FolderFolio.Indexing;

public interface IPhotoScanFileSystem
{
    bool DirectoryExists(string path);

    IEnumerable<string> EnumerateDirectories(string path);

    IEnumerable<string> EnumerateFiles(string path);

    FileAttributes GetAttributes(string path);
}

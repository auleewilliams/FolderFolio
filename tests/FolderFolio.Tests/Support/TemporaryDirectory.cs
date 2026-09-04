namespace FolderFolio.Tests.Support;

public sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "FolderFolio.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string CreateDirectory(string relativePath)
    {
        var path = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(path);
        return path;
    }

    public string FilePath(string relativePath) => System.IO.Path.Combine(Path, relativePath);

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}

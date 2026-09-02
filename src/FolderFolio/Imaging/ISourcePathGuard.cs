using FolderFolio.Domain;

namespace FolderFolio.Imaging;

public interface ISourcePathGuard
{
    bool TryResolve(IndexedPhoto photo, out string sourcePath);
}

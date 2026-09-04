namespace FolderFolio.Indexing;

public interface IPhotoRootWatcher : IDisposable
{
    void Start();

    void Stop();
}

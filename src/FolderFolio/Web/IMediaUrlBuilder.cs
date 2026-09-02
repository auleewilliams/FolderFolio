using FolderFolio.Domain;
using FolderFolio.Imaging;

namespace FolderFolio.Web;

public interface IMediaUrlBuilder
{
    string Build(IndexedAlbum album, IndexedPhoto photo, DerivativeKind kind);
}

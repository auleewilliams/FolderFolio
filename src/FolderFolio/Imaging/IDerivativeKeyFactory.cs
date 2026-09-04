using FolderFolio.Domain;

namespace FolderFolio.Imaging;

public interface IDerivativeKeyFactory
{
    DerivativeIdentity Create(IndexedPhoto photo, DerivativeKind kind);
}

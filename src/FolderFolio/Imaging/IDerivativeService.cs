using FolderFolio.Domain;

namespace FolderFolio.Imaging;

public interface IDerivativeService
{
    Task<CachedDerivative> GetOrCreateAsync(
        IndexedPhoto photo,
        DerivativeKind kind,
        CancellationToken cancellationToken);
}

using FolderFolio.Domain;
using FolderFolio.Imaging;

namespace FolderFolio.Tests.Support;

public sealed class StubDerivativeService : IDerivativeService
{
    public required CachedDerivative Derivative { get; init; }

    public bool ThrowStaleSource { get; set; }

    public Task<CachedDerivative> GetOrCreateAsync(
        IndexedPhoto photo,
        DerivativeKind kind,
        CancellationToken cancellationToken)
    {
        if (ThrowStaleSource)
        {
            throw new StaleSourceException();
        }

        return Task.FromResult(Derivative);
    }
}

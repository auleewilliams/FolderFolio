using FolderFolio.Domain;
using FolderFolio.Imaging;

namespace FolderFolio.Tests.Support;

public sealed class StubDerivativeService : IDerivativeService
{
    public required CachedDerivative Derivative { get; init; }

    public bool ThrowStaleSource { get; set; }

    public Exception? Failure { get; set; }

    public Task<CachedDerivative> GetOrCreateAsync(
        IndexedPhoto photo,
        DerivativeKind kind,
        CancellationToken cancellationToken)
    {
        if (ThrowStaleSource)
        {
            throw new StaleSourceException();
        }

        if (Failure is not null)
        {
            throw Failure;
        }

        return Task.FromResult(Derivative);
    }
}

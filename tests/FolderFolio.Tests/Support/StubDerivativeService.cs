using FolderFolio.Domain;
using FolderFolio.Imaging;

namespace FolderFolio.Tests.Support;

public sealed class StubDerivativeService : IDerivativeService
{
    public required CachedDerivative Derivative { get; init; }

    public bool ThrowStaleSource { get; set; }

    public Exception? Failure { get; set; }
    public Dictionary<DerivativeKind, Exception> Failures { get; } = [];
    public List<DerivativeKind> RequestedKinds { get; } = [];

    public Task<CachedDerivative> GetOrCreateAsync(
        IndexedPhoto photo,
        DerivativeKind kind,
        CancellationToken cancellationToken)
    {
        RequestedKinds.Add(kind);
        if (ThrowStaleSource)
        {
            throw new StaleSourceException();
        }

        if (Failure is not null)
        {
            throw Failure;
        }

        if (Failures.TryGetValue(kind, out var failure)) throw failure;

        return Task.FromResult(Derivative);
    }
}

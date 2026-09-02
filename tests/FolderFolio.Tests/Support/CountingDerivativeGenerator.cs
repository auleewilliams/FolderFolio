using FolderFolio.Domain;
using FolderFolio.Imaging;

namespace FolderFolio.Tests.Support;

public sealed class CountingDerivativeGenerator : IImageDerivativeGenerator
{
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Exception? _failure;

    public CountingDerivativeGenerator(Exception? failure = null)
    {
        _failure = failure;
    }

    public int Count { get; private set; }

    public void Release() => _release.TrySetResult();

    public async Task WriteWebPAsync(
        IndexedPhoto photo,
        Stream destination,
        int maxLongEdge,
        int quality,
        CancellationToken cancellationToken)
    {
        Count++;
        await _release.Task.WaitAsync(cancellationToken);
        if (_failure is not null)
        {
            throw _failure;
        }

        await destination.WriteAsync(new byte[] { 1, 2, 3, 4 }, cancellationToken);
    }
}

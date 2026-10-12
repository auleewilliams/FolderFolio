using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using FolderFolio.Domain;

namespace FolderFolio.Imaging;

public sealed class ImageSharpDerivativeGenerator : IImageDerivativeGenerator
{
    private readonly ISourcePathGuard _sourcePathGuard;

    public ImageSharpDerivativeGenerator(ISourcePathGuard sourcePathGuard)
    {
        _sourcePathGuard = sourcePathGuard ?? throw new ArgumentNullException(nameof(sourcePathGuard));
    }

    public Task WriteWebPAsync(IndexedPhoto photo, Stream destination, int maxLongEdge, int quality, CancellationToken cancellationToken) =>
        WriteAsync(photo, destination, maxLongEdge, quality, false, cancellationToken);

    public Task WriteAvifAsync(IndexedPhoto photo, Stream destination, int maxLongEdge, int quality, CancellationToken cancellationToken) =>
        WriteAsync(photo, destination, maxLongEdge, quality, true, cancellationToken);

    private async Task WriteAsync(
        IndexedPhoto photo,
        Stream destination,
        int maxLongEdge,
        int quality,
        bool avif,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(photo);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxLongEdge, 0);
        ArgumentOutOfRangeException.ThrowIfNegative(quality);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(quality, 100);

        if (!_sourcePathGuard.TryResolve(photo, out var sourcePath))
        {
            throw new StaleSourceException();
        }

        if (AvifCodec.IsAvif(sourcePath))
        {
            Image decoded;
            try
            {
                decoded = await AvifCodec.LoadAsync(sourcePath, maxLongEdge, cancellationToken);
            }
            catch (Exception exception) when (IsExpectedSourceReadFailure(exception))
            {
                ThrowIfSourceBecameStale(photo);
                throw;
            }
            using (decoded) await EncodeAsync(decoded, destination, quality, avif, cancellationToken);
            return;
        }

        var identificationOptions = new DecoderOptions
        {
            MaxFrames = 1,
            SkipMetadata = false
        };
        ImageInfo sourceInfo;
        try
        {
            sourceInfo = await Image.IdentifyAsync(identificationOptions, sourcePath, cancellationToken);
        }
        catch (Exception exception) when (IsExpectedSourceReadFailure(exception))
        {
            ThrowIfSourceBecameStale(photo);
            throw;
        }

        var decoderOptions = sourceInfo.Width > maxLongEdge || sourceInfo.Height > maxLongEdge
            ? new DecoderOptions
            {
                MaxFrames = 1,
                SkipMetadata = false,
                TargetSize = new Size(maxLongEdge, maxLongEdge)
            }
            : new DecoderOptions
            {
                MaxFrames = 1,
                SkipMetadata = false
            };

        Image image;
        try
        {
            image = await Image.LoadAsync(decoderOptions, sourcePath, cancellationToken);
        }
        catch (Exception exception) when (IsExpectedSourceReadFailure(exception))
        {
            ThrowIfSourceBecameStale(photo);
            throw;
        }

        using (image)
        {
            image.Mutate(context => context.AutoOrient());
            if (image.Width > maxLongEdge || image.Height > maxLongEdge)
            {
                image.Mutate(context => context.Resize(new ResizeOptions
                {
                    Size = new Size(maxLongEdge, maxLongEdge),
                    Mode = ResizeMode.Max
                }));
            }
            image.Metadata.ExifProfile = null;
            image.Metadata.IptcProfile = null;
            image.Metadata.XmpProfile = null;

            await EncodeAsync(image, destination, quality, avif, cancellationToken);
        }
    }

    private static async Task EncodeAsync(Image image, Stream destination, int quality, bool avif, CancellationToken cancellationToken)
    {
        if (avif)
        {
            await AvifCodec.WriteAsync(image, destination, quality, cancellationToken);
        }
        else
        {
            var encoder = new WebpEncoder
            {
                FileFormat = WebpFileFormatType.Lossy,
                Quality = quality,
                SkipMetadata = true
            };
            await image.SaveAsWebpAsync(destination, encoder, cancellationToken);
        }
    }

    private static bool IsExpectedSourceReadFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or ImageFormatException;

    private void ThrowIfSourceBecameStale(IndexedPhoto photo)
    {
        if (!_sourcePathGuard.TryResolve(photo, out _))
        {
            throw new StaleSourceException();
        }
    }
}

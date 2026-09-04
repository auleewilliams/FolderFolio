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

    public async Task WriteWebPAsync(
        IndexedPhoto photo,
        Stream destination,
        int maxLongEdge,
        int quality,
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

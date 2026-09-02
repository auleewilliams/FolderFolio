using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace FolderFolio.Imaging;

public sealed class ImageSharpDerivativeGenerator : IImageDerivativeGenerator
{
    public async Task WriteWebPAsync(
        string sourcePath,
        Stream destination,
        int maxLongEdge,
        int quality,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxLongEdge, 0);
        ArgumentOutOfRangeException.ThrowIfNegative(quality);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(quality, 100);

        var sourceInfo = await Image.IdentifyAsync(sourcePath, cancellationToken);
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

        using var image = await Image.LoadAsync(decoderOptions, sourcePath, cancellationToken);
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

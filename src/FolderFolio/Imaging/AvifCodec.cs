using System.Globalization;
using FolderFolio.Indexing;
using ImageMagick;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using Image = SixLabors.ImageSharp.Image;

namespace FolderFolio.Imaging;

internal static class AvifCodec
{
    public static bool IsAvif(string path) => string.Equals(Path.GetExtension(path), ".avif", StringComparison.OrdinalIgnoreCase);

    public static async Task<PhotoSourceMetadata> IdentifyAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var image = new MagickImage();
            await image.PingAsync(new FileInfo(path), ReadSettings(), cancellationToken);
            if (image.Width > int.MaxValue || image.Height > int.MaxValue)
                throw new InvalidImageContentException("AVIF dimensions exceed the supported range.");
            var width = (int)image.Width;
            var height = (int)image.Height;
            if ((int)image.Orientation is >= 5 and <= 8) (width, height) = (height, width);
            var exif = image.GetExifProfile();
            var capturedAt = ParseDate(exif?.GetValue(ImageMagick.ExifTag.DateTimeOriginal)?.Value)
                ?? ParseDate(exif?.GetValue(ImageMagick.ExifTag.DateTimeDigitized)?.Value);
            var estimatedBytes = (decimal)width * height * 4;
            return new PhotoSourceMetadata(width, height, capturedAt, estimatedBytes >= long.MaxValue ? long.MaxValue : (long)estimatedBytes);
        }
        catch (MagickException exception)
        {
            throw new InvalidImageContentException("AVIF metadata could not be read.", exception);
        }
    }

    public static async Task<Image> LoadAsync(string path, int maxLongEdge, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var image = new MagickImage();
            await image.ReadAsync(new FileInfo(path), ReadSettings(), cancellationToken);
            image.AutoOrient();
            if (image.Width > maxLongEdge || image.Height > maxLongEdge)
                image.Resize(new MagickGeometry((uint)maxLongEdge, (uint)maxLongEdge));
            image.Strip();
            await using var png = new MemoryStream();
            await image.WriteAsync(png, MagickFormat.Png, cancellationToken);
            png.Position = 0;
            return await Image.LoadAsync(png, cancellationToken);
        }
        catch (MagickException exception)
        {
            throw new InvalidImageContentException("AVIF source could not be decoded.", exception);
        }
    }

    public static async Task WriteAsync(Image image, Stream destination, int quality, CancellationToken cancellationToken)
    {
        await using var png = new MemoryStream();
        await image.SaveAsPngAsync(png, new PngEncoder { SkipMetadata = true }, cancellationToken);
        png.Position = 0;
        using var encoded = new MagickImage();
        await encoded.ReadAsync(png, new MagickReadSettings { Format = MagickFormat.Png, FrameCount = 1 }, cancellationToken);
        encoded.Strip();
        encoded.Quality = (uint)quality;
        await encoded.WriteAsync(destination, MagickFormat.Avif, cancellationToken);
    }

    private static MagickReadSettings ReadSettings() => new() { Format = MagickFormat.Avif, FrameCount = 1 };

    private static DateTime? ParseDate(string? value) => DateTime.TryParseExact(value,
        "yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var capturedAt) ? capturedAt : null;
}

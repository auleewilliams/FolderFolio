using System.Globalization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;

namespace FolderFolio.Indexing;

public sealed class ImageSharpMetadataReader : IImageMetadataReader
{
    private const string ExifDateTimeFormat = "yyyy:MM:dd HH:mm:ss";

    public async Task<PhotoSourceMetadata> IdentifyAsync(
        string sourcePath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        var options = new DecoderOptions
        {
            MaxFrames = 1,
            SkipMetadata = false
        };
        var info = await Image.IdentifyAsync(options, sourcePath, cancellationToken);
        var (width, height) = GetLogicalDimensions(info);
        var estimatedPixelBytes = GetEstimatedPixelBytes(info);

        return new PhotoSourceMetadata(width, height, GetCapturedAt(info.Metadata.ExifProfile), estimatedPixelBytes);
    }

    private static (int Width, int Height) GetLogicalDimensions(ImageInfo info)
    {
        var orientation = GetOrientation(info.Metadata.ExifProfile);
        return orientation is >= 5 and <= 8
            ? (info.Height, info.Width)
            : (info.Width, info.Height);
    }

    private static long GetEstimatedPixelBytes(ImageInfo info)
    {
        var bytes = decimal.Ceiling((decimal)info.Width * info.Height * info.PixelType.BitsPerPixel / 8);
        return bytes >= long.MaxValue ? long.MaxValue : decimal.ToInt64(bytes);
    }

    private static DateTime? GetCapturedAt(ExifProfile? exif) =>
        ParseExifDate(exif, ExifTag.DateTimeOriginal) ?? ParseExifDate(exif, ExifTag.DateTimeDigitized);

    private static DateTime? ParseExifDate(ExifProfile? exif, ExifTag<string> tag)
    {
        if (exif is not null && exif.TryGetValue(tag, out var value) &&
            DateTime.TryParseExact(
                value.Value,
                ExifDateTimeFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var capturedAt))
        {
            return capturedAt;
        }

        return null;
    }

    private static ushort? GetOrientation(ExifProfile? exif) =>
        exif is not null && exif.TryGetValue(ExifTag.Orientation, out var value)
            ? Convert.ToUInt16(value.Value, CultureInfo.InvariantCulture)
            : null;
}

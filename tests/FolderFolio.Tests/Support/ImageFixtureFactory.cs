using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Iptc;
using SixLabors.ImageSharp.Metadata.Profiles.Xmp;
using SixLabors.ImageSharp.PixelFormats;

namespace FolderFolio.Tests.Support;

public static class ImageFixtureFactory
{
    public static void CreateJpeg(
        string path,
        int width = 120,
        int height = 80,
        string? dateTimeOriginal = "2024:03:02 10:11:12",
        string? dateTimeDigitized = null,
        ushort? orientation = null,
        bool includePrivateMetadata = false)
    {
        using var image = new Image<Rgba32>(width, height);
        var exif = image.Metadata.ExifProfile ??= new ExifProfile();

        if (dateTimeOriginal is not null)
        {
            exif.SetValue(ExifTag.DateTimeOriginal, dateTimeOriginal);
        }

        if (dateTimeDigitized is not null)
        {
            exif.SetValue(ExifTag.DateTimeDigitized, dateTimeDigitized);
        }

        if (orientation is not null)
        {
            exif.SetValue(ExifTag.Orientation, orientation.Value);
        }

        if (includePrivateMetadata)
        {
            exif.SetValue(ExifTag.GPSLatitudeRef, "S");
            var iptc = new IptcProfile();
            iptc.SetValue(IptcTag.Caption, "Private caption");
            image.Metadata.IptcProfile = iptc;
            image.Metadata.XmpProfile = new XmpProfile("<x:xmpmeta xmlns:x=\"adobe:ns:meta/\">private</x:xmpmeta>"u8.ToArray());
        }

        image.SaveAsJpeg(path);
    }
}

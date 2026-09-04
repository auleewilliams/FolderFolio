using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FolderFolio.Configuration;
using FolderFolio.Domain;

namespace FolderFolio.Imaging;

public sealed class DerivativeKeyFactory : IDerivativeKeyFactory
{
    private const int VersionLength = 24;
    private readonly FolderFolioOptions _options;
    private readonly int _cacheSchema;

    public DerivativeKeyFactory(FolderFolioOptions options, int cacheSchema = 1)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(cacheSchema, 0);

        _options = options;
        _cacheSchema = cacheSchema;
    }

    public DerivativeIdentity Create(IndexedPhoto photo, DerivativeKind kind)
    {
        ArgumentNullException.ThrowIfNull(photo);

        var (maxLongEdge, kindName) = kind switch
        {
            DerivativeKind.Grid => (_options.GridLongEdge, "grid"),
            DerivativeKind.Web => (_options.WebLongEdge, "web"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        var relativePath = photo.Source.RelativePath.Replace('\\', '/');
        var bytes = CanonicalBytes(relativePath, photo.Source.Length, photo.Source.LastWriteUtcTicks, kindName, maxLongEdge);
        var cacheKey = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        return new DerivativeIdentity(
            cacheKey,
            cacheKey[..VersionLength],
            $"\"{cacheKey}\"",
            maxLongEdge,
            _options.WebPQuality);
    }

    private byte[] CanonicalBytes(string relativePath, long length, long lastWriteUtcTicks, string kind, int maxLongEdge)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("cacheSchema", _cacheSchema);
            writer.WriteString("relativePath", relativePath);
            writer.WriteNumber("length", length);
            writer.WriteNumber("lastWriteUtcTicks", lastWriteUtcTicks);
            writer.WriteString("kind", kind);
            writer.WriteNumber("maxLongEdge", maxLongEdge);
            writer.WriteNumber("webPQuality", _options.WebPQuality);
            writer.WriteString("format", "webp");
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }
}

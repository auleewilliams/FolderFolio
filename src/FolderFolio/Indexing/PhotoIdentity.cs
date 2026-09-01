using System.Security.Cryptography;
using System.Text;

namespace FolderFolio.Indexing;

public static class PhotoIdentity
{
    public static string FromRelativePath(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);

        var normalizedPath = relativePath.Replace('\\', '/');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedPath))).ToLowerInvariant();
    }
}

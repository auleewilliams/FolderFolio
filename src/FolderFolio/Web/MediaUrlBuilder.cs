using FolderFolio.Domain;
using FolderFolio.Imaging;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;

namespace FolderFolio.Web;

public sealed class MediaUrlBuilder(LinkGenerator links, IDerivativeKeyFactory keyFactory) : IMediaUrlBuilder
{
    public string Build(IndexedAlbum album, IndexedPhoto photo, DerivativeKind kind)
    {
        ArgumentNullException.ThrowIfNull(album);
        ArgumentNullException.ThrowIfNull(photo);

        var size = kind switch
        {
            DerivativeKind.Grid => "grid",
            DerivativeKind.Web => "web",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        var path = links.GetPathByName(
            MediaEndpoint.RouteName,
            new { albumSlug = album.Slug, photoId = photo.Id, size })
            ?? throw new InvalidOperationException("The media endpoint route is not available.");

        return QueryHelpers.AddQueryString(path, "v", keyFactory.Create(photo, kind).Version);
    }
}

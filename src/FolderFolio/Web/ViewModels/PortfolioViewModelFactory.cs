using FolderFolio.Domain;
using FolderFolio.Imaging;

namespace FolderFolio.Web.ViewModels;

public sealed class PortfolioViewModelFactory(IMediaUrlBuilder mediaUrls)
{
    public IReadOnlyList<AlbumCardViewModel> CreateAlbumCards(PortfolioSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return snapshot.Albums
            .Select(CreateAlbumCard)
            .ToArray();
    }

    public AlbumGalleryViewModel CreateGallery(IndexedAlbum album)
    {
        ArgumentNullException.ThrowIfNull(album);

        var photoCount = album.Photos.Length;
        var photos = album.Photos
            .Select((photo, zeroBasedPosition) => CreatePhoto(album, photo, zeroBasedPosition + 1, photoCount))
            .ToArray();

        return new AlbumGalleryViewModel(album.Slug, album.Title, photoCount, photos);
    }

    private AlbumCardViewModel CreateAlbumCard(IndexedAlbum album)
    {
        var cover = album.Cover;
        return cover is null
            ? new AlbumCardViewModel(album.Slug, album.Title, 0, string.Empty, 0, 0)
            : new AlbumCardViewModel(
                album.Slug,
                album.Title,
                album.Photos.Length,
                mediaUrls.Build(album, cover, DerivativeKind.Grid),
                cover.Width,
                cover.Height);
    }

    private PhotoViewModel CreatePhoto(IndexedAlbum album, IndexedPhoto photo, int position, int photoCount) => new(
        photo.Id,
        $"Photo {position} of {photoCount} in {album.Title}",
        mediaUrls.Build(album, photo, DerivativeKind.Grid),
        mediaUrls.Build(album, photo, DerivativeKind.Web),
        photo.Width,
        photo.Height,
        position,
        position <= 4);
}

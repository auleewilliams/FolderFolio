namespace FolderFolio.Web.ViewModels;

public sealed record AlbumGalleryViewModel(
    string Slug,
    string Title,
    int PhotoCount,
    IReadOnlyList<PhotoViewModel> Photos);

namespace FolderFolio.Web.ViewModels;

public sealed record AlbumCardViewModel(
    string Slug,
    string Title,
    int PhotoCount,
    string CoverUrl,
    int CoverWidth,
    int CoverHeight);

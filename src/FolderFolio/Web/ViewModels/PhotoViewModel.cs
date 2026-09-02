namespace FolderFolio.Web.ViewModels;

public sealed record PhotoViewModel(
    string Id,
    string AccessibleName,
    string GridUrl,
    string WebUrl,
    int Width,
    int Height,
    int Position,
    bool LoadEagerly);

using FolderFolio.Configuration;
using FolderFolio.Indexing;
using FolderFolio.Web.ViewModels;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FolderFolio.Pages;

public sealed class IndexModel(
    IPortfolioIndex portfolioIndex,
    FolderFolioOptions options,
    PortfolioViewModelFactory viewModels) : PageModel
{
    public string SiteTitle { get; private set; } = string.Empty;

    public string Tagline { get; private set; } = string.Empty;

    public IndexPageState PageState { get; private set; }

    public IReadOnlyList<AlbumCardViewModel> Albums { get; private set; } = [];

    public void OnGet()
    {
        var publication = portfolioIndex.Current;
        SiteTitle = options.SiteTitle;
        Tagline = options.Tagline;

        if (publication.LastSuccessAtUtc is null)
        {
            PageState = IndexPageState.Preparing;
            Albums = [];
            return;
        }

        Albums = viewModels.CreateAlbumCards(publication.Snapshot);
        PageState = Albums.Count == 0 ? IndexPageState.Empty : IndexPageState.Populated;
    }
}

using FolderFolio.Indexing;
using FolderFolio.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FolderFolio.Pages.Albums;

public sealed class DetailsModel(
    IPortfolioIndex portfolioIndex,
    PortfolioViewModelFactory viewModels) : PageModel
{
    public bool IsPreparing { get; private set; }

    public AlbumGalleryViewModel? Gallery { get; private set; }

    public IActionResult OnGet(string slug)
    {
        var publication = portfolioIndex.Current;
        if (publication.LastSuccessAtUtc is null)
        {
            IsPreparing = true;
            return Page();
        }

        return publication.Snapshot.AlbumsBySlug.TryGetValue(slug, out var album)
            ? PageForAlbum(album)
            : NotFound();
    }

    private PageResult PageForAlbum(FolderFolio.Domain.IndexedAlbum album)
    {
        Gallery = viewModels.CreateGallery(album);
        return Page();
    }
}

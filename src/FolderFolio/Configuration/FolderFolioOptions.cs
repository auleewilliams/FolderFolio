namespace FolderFolio.Configuration;

public sealed class FolderFolioOptions
{
    public const string SectionName = "FolderFolio";

    public string PhotoRoot { get; set; } = "/photos";
    public string CacheRoot { get; set; } = "/cache";
    public int GridLongEdge { get; set; } = 400;
    public int WebLongEdge { get; set; } = 2000;
    public int WebPQuality { get; set; } = 82;
    public int WatcherDebounceMilliseconds { get; set; } = 2000;
    public string SiteTitle { get; set; } = "FolderFolio";
    public string? SiteUrl { get; set; }
    public string Tagline { get; set; } = "Photos from a folder.";
}

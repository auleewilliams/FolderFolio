namespace FolderFolio.Web;

public static class RobotsEndpoint
{
    public static void Map(WebApplication app) => app.MapGet(
        "/robots.txt",
        () => Results.Text("User-agent: *\nDisallow: /\n", "text/plain"));
}

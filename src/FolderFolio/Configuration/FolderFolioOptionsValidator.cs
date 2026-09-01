using Microsoft.Extensions.Options;

namespace FolderFolio.Configuration;

public sealed class FolderFolioOptionsValidator : IValidateOptions<FolderFolioOptions>
{
    public ValidateOptionsResult Validate(string? name, FolderFolioOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.PhotoRoot) || !Path.IsPathRooted(options.PhotoRoot))
        {
            failures.Add($"{nameof(options.PhotoRoot)} must be an absolute path.");
        }

        if (string.IsNullOrWhiteSpace(options.CacheRoot) || !Path.IsPathRooted(options.CacheRoot))
        {
            failures.Add($"{nameof(options.CacheRoot)} must be an absolute path.");
        }

        if (options.GridLongEdge <= 0)
        {
            failures.Add($"{nameof(options.GridLongEdge)} must be greater than zero.");
        }

        if (options.WebLongEdge <= 0 || options.WebLongEdge < options.GridLongEdge)
        {
            failures.Add($"{nameof(options.WebLongEdge)} must be greater than or equal to {nameof(options.GridLongEdge)}.");
        }

        if (options.WebPQuality is < 1 or > 100)
        {
            failures.Add($"{nameof(options.WebPQuality)} must be between 1 and 100.");
        }

        if (string.IsNullOrWhiteSpace(options.SiteTitle))
        {
            failures.Add($"{nameof(options.SiteTitle)} must not be empty.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace FolderFolio.Indexing;

public static partial class AlbumNameParser
{
    private static readonly Regex OrderedNamePattern = OrderedNameRegex();
    private static readonly Regex DisplaySeparatorPattern = DisplaySeparatorRegex();

    public static AlbumNameInfo Parse(string directoryName)
    {
        var candidate = directoryName ?? string.Empty;
        var match = OrderedNamePattern.Match(candidate);
        var sortPrefix = default(int?);

        if (match.Success && int.TryParse(match.Groups["order"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedPrefix))
        {
            sortPrefix = parsedPrefix;
            candidate = match.Groups["title"].Value;
        }

        var title = DisplaySeparatorPattern.Replace(candidate, " ").Trim();
        if (title.Length == 0)
        {
            title = "Album";
        }

        return new AlbumNameInfo(sortPrefix, title, CreateSlug(title));
    }

    private static string CreateSlug(string title)
    {
        var slug = new StringBuilder();
        var pendingHyphen = false;

        foreach (var character in title.Normalize(NormalizationForm.FormD))
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark)
            {
                continue;
            }

            if (character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                if (pendingHyphen && slug.Length > 0)
                {
                    slug.Append('-');
                }

                slug.Append(char.ToLowerInvariant(character));
                pendingHyphen = false;
            }
            else
            {
                pendingHyphen = slug.Length > 0;
            }
        }

        return slug.Length == 0 ? "album" : slug.ToString();
    }

    [GeneratedRegex("^(?<order>\\d+)[-_](?<title>.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex OrderedNameRegex();

    [GeneratedRegex("[-_\\s]+", RegexOptions.CultureInvariant)]
    private static partial Regex DisplaySeparatorRegex();
}

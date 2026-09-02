using System.Collections.Immutable;

namespace FolderFolio.Indexing;

public sealed record IndexRefreshRequest(bool FullScan, ImmutableHashSet<string> AlbumDirectoryNames)
{
    public static IndexRefreshRequest Full { get; } =
        new(true, ImmutableHashSet<string>.Empty.WithComparer(StringComparer.Ordinal));

    public static IndexRefreshRequest Album(string albumDirectoryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(albumDirectoryName);

        return new(false, ImmutableHashSet.Create(StringComparer.Ordinal, albumDirectoryName));
    }
}

namespace FolderFolio.Domain;

public sealed record IndexPublication(
    long Generation,
    IndexStatus Status,
    PortfolioSnapshot Snapshot,
    DateTimeOffset? LastSuccessAtUtc,
    TimeSpan? LastSuccessDuration,
    string? PublicError);

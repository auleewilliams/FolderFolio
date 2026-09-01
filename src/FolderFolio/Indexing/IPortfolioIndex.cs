using FolderFolio.Domain;

namespace FolderFolio.Indexing;

public interface IPortfolioIndex
{
    IndexPublication Current { get; }

    void PublishReady(PortfolioSnapshot snapshot, DateTimeOffset completedAtUtc, TimeSpan duration);

    void MarkStarting(string? publicError = null);

    void MarkDegraded(string publicError);
}

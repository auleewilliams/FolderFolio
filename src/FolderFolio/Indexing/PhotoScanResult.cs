using FolderFolio.Domain;

namespace FolderFolio.Indexing;

public sealed record PhotoScanResult(PortfolioSnapshot Snapshot, int SkippedFileCount);

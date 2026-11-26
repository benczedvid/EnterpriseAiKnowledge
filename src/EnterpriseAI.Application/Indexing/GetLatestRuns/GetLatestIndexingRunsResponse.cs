namespace EnterpriseAI.Application.Indexing.GetLatestRuns;

public sealed record GetLatestIndexingRunsResponse(
    int Count,
    IReadOnlyList<IndexingRunResponse> Runs);

public sealed record IndexingRunResponse(
    Guid Id,
    string SourceType,
    string? FolderPath,
    int RequestedMaximumDocuments,
    bool ForceReindex,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    double? DurationSeconds,
    int DiscoveredCount,
    int ProcessedCount,
    int IndexedCount,
    int SkippedUnchangedCount,
    int SkippedUnsupportedCount,
    int RequiresOcrCount,
    int FailedCount,
    int NotFoundCount,
    int TotalChunkCount,
    string? FailureMessage);
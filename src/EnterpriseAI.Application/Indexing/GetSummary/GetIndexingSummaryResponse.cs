namespace EnterpriseAI.Application.Indexing.GetSummary;

public sealed record GetIndexingSummaryResponse(
    DocumentIndexingSummaryResponse Documents,
    ChunkIndexingSummaryResponse Chunks,
    LatestIndexingRunSummaryResponse? LatestRun);

public sealed record DocumentIndexingSummaryResponse(
    int Total,
    int Indexed,
    int Processing,
    int Failed,
    int RequiresOcr,
    int Deleted);

public sealed record ChunkIndexingSummaryResponse(
    int Total);

public sealed record LatestIndexingRunSummaryResponse(
    Guid Id,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    double? DurationSeconds,
    int ProcessedCount,
    int IndexedCount,
    int SkippedUnchangedCount,
    int FailedCount);

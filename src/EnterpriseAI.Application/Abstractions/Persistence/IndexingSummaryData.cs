namespace EnterpriseAI.Application.Abstractions.Persistence;

public sealed record IndexingSummaryData(
    int TotalDocumentCount,
    int IndexedDocumentCount,
    int ProcessingDocumentCount,
    int FailedDocumentCount,
    int RequiresOcrDocumentCount,
    int DeletedDocumentCount,
    int TotalChunkCount,
    LatestIndexingRunData? LatestRun);

public sealed record LatestIndexingRunData(
    Guid Id,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    int ProcessedCount,
    int IndexedCount,
    int SkippedUnchangedCount,
    int FailedCount);
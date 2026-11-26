namespace EnterpriseAI.Application.Documents.IndexSharePointBatch;

public sealed record IndexSharePointBatchResponse(
    Guid IndexingRunId,
    int DiscoveredCount,
    int ProcessedCount,
    int IndexedCount,
    int SkippedUnchangedCount,
    int SkippedUnsupportedCount,
    int RequiresOcrCount,
    int FailedCount,
    int NotFoundCount,
    int TotalChunkCount,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    IReadOnlyList<IndexSharePointBatchItemResponse> Items)
{
    public TimeSpan Duration => CompletedAt - StartedAt;
}
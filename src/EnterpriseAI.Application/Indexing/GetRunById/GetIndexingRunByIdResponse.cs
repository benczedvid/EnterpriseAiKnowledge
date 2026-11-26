namespace EnterpriseAI.Application.Indexing.GetRunById;

public sealed record GetIndexingRunByIdResponse(bool Found, IndexingRunDetailsResponse? Run)
{
    public static GetIndexingRunByIdResponse NotFound()
    {
        return new GetIndexingRunByIdResponse(
            Found: false,
            Run: null);
    }

    public static GetIndexingRunByIdResponse Success(IndexingRunDetailsResponse run)
    {
        ArgumentNullException.ThrowIfNull(run);

        return new GetIndexingRunByIdResponse(
            Found: true,
            Run: run);
    }
}

public sealed record IndexingRunDetailsResponse(
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
    string? FailureMessage,
    IReadOnlyList<IndexingRunItemDetailsResponse> Items);

public sealed record IndexingRunItemDetailsResponse(
    Guid Id,
    string SourceId,
    string? FileName,
    string Status,
    string Message,
    Guid? DocumentId,
    int ChunkCount,
    int CharacterCount,
    DateTimeOffset? IndexedAt,
    DateTimeOffset CreatedAt);
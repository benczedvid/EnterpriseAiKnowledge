using EnterpriseAI.Application.Abstractions.Persistence;
using EnterpriseAI.Domain.Indexing;

namespace EnterpriseAI.Application.Indexing.GetRunById;

public sealed class GetIndexingRunByIdHandler
{
    private readonly IIndexingRunRepository _indexingRunRepository;

    public GetIndexingRunByIdHandler(IIndexingRunRepository indexingRunRepository)
    {
        _indexingRunRepository = indexingRunRepository;
    }

    public async Task<GetIndexingRunByIdResponse> HandleAsync(
        GetIndexingRunByIdRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.IndexingRunId == Guid.Empty)
        {
            throw new ArgumentException("Indexing run ID must not be empty.", nameof(request.IndexingRunId));
        }

        var run = await _indexingRunRepository.GetByIdAsync(request.IndexingRunId, cancellationToken);

        if (run is null)
        {
            return GetIndexingRunByIdResponse.NotFound();
        }

        return GetIndexingRunByIdResponse.Success(MapRun(run));
    }

    private static IndexingRunDetailsResponse MapRun(IndexingRun run)
    {
        return new IndexingRunDetailsResponse(
            Id: run.Id,
            SourceType: run.SourceType,
            FolderPath: run.FolderPath,
            RequestedMaximumDocuments: run.RequestedMaximumDocuments,
            ForceReindex: run.ForceReindex,
            Status: run.Status.ToString(),
            StartedAt: run.StartedAt,
            CompletedAt: run.CompletedAt,
            DurationSeconds: run.Duration.HasValue
                ? Math.Round(
                    run.Duration.Value.TotalSeconds,
                    1)
                : null,
            DiscoveredCount: run.DiscoveredCount,
            ProcessedCount: run.ProcessedCount,
            IndexedCount: run.IndexedCount,
            SkippedUnchangedCount: run.SkippedUnchangedCount,
            SkippedUnsupportedCount: run.SkippedUnsupportedCount,
            RequiresOcrCount: run.RequiresOcrCount,
            FailedCount: run.FailedCount,
            NotFoundCount: run.NotFoundCount,
            TotalChunkCount: run.TotalChunkCount,
            FailureMessage: run.FailureMessage,
            Items: run.Items
                .OrderBy(item => item.CreatedAt)
                .Select(item =>
                    new IndexingRunItemDetailsResponse(
                        Id: item.Id,
                        SourceId: item.SourceId,
                        FileName: item.FileName,
                        Status: item.Status.ToString(),
                        Message: item.Message,
                        DocumentId: item.DocumentId,
                        ChunkCount: item.ChunkCount,
                        CharacterCount: item.CharacterCount,
                        IndexedAt: item.IndexedAt,
                        CreatedAt: item.CreatedAt))
                .ToArray());
    }
}
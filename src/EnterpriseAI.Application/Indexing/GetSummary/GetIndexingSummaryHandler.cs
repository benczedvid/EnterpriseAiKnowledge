using EnterpriseAI.Application.Abstractions.Persistence;

namespace EnterpriseAI.Application.Indexing.GetSummary;

public sealed class GetIndexingSummaryHandler
{
    private readonly IIndexingSummaryRepository
        _summaryRepository;

    public GetIndexingSummaryHandler(
        IIndexingSummaryRepository summaryRepository)
    {
        _summaryRepository = summaryRepository;
    }

    public async Task<GetIndexingSummaryResponse> HandleAsync(
        CancellationToken cancellationToken)
    {
        var summary = await _summaryRepository.GetAsync(
            cancellationToken);

        LatestIndexingRunSummaryResponse? latestRun = null;

        if (summary.LatestRun is not null)
        {
            var duration = summary.LatestRun.CompletedAt.HasValue
                ? summary.LatestRun.CompletedAt.Value
                    - summary.LatestRun.StartedAt
                : (TimeSpan?)null;

            latestRun = new LatestIndexingRunSummaryResponse(
                Id: summary.LatestRun.Id,
                Status: summary.LatestRun.Status,
                StartedAt: summary.LatestRun.StartedAt,
                CompletedAt: summary.LatestRun.CompletedAt,
                DurationSeconds: duration.HasValue
                    ? Math.Round(
                        duration.Value.TotalSeconds,
                        1)
                    : null,
                ProcessedCount:
                    summary.LatestRun.ProcessedCount,
                IndexedCount:
                    summary.LatestRun.IndexedCount,
                SkippedUnchangedCount:
                    summary.LatestRun
                        .SkippedUnchangedCount,
                FailedCount:
                    summary.LatestRun.FailedCount);
        }

        return new GetIndexingSummaryResponse(
            Documents: new DocumentIndexingSummaryResponse(
                Total: summary.TotalDocumentCount,
                Indexed: summary.IndexedDocumentCount,
                Processing:
                    summary.ProcessingDocumentCount,
                Failed: summary.FailedDocumentCount,
                RequiresOcr:
                    summary.RequiresOcrDocumentCount,
                Deleted: summary.DeletedDocumentCount),
            Chunks: new ChunkIndexingSummaryResponse(
                Total: summary.TotalChunkCount),
            LatestRun: latestRun);
    }
}
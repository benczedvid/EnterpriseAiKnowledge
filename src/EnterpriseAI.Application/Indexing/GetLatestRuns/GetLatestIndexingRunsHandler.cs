using EnterpriseAI.Application.Abstractions.Persistence;
using EnterpriseAI.Domain.Indexing;

namespace EnterpriseAI.Application.Indexing.GetLatestRuns;

public sealed class GetLatestIndexingRunsHandler
{
    private const int MaximumLimit = 100;
    private readonly IIndexingRunRepository _indexingRunRepository;

    public GetLatestIndexingRunsHandler(IIndexingRunRepository indexingRunRepository)
    {
        _indexingRunRepository = indexingRunRepository;
    }

    public async Task<GetLatestIndexingRunsResponse> HandleAsync(
        GetLatestIndexingRunsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Limit is < 1 or > MaximumLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.Limit),
                $"Limit must be between 1 and {MaximumLimit}.");
        }

        var runs = await _indexingRunRepository.GetLatestAsync(request.Limit, cancellationToken);

        var responseRuns = runs
            .Select(MapRun)
            .ToArray();

        return new GetLatestIndexingRunsResponse(
            Count: responseRuns.Length,
            Runs: responseRuns);
    }

    private static IndexingRunResponse MapRun(IndexingRun run)
    {
        return new IndexingRunResponse(
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
            FailureMessage: run.FailureMessage);
    }
}
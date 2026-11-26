using EnterpriseAI.Application.Abstractions.Persistence;
using EnterpriseAI.Domain.Documents;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAI.Infrastructure.Persistence.Repositories;

public sealed class EfIndexingSummaryRepository : IIndexingSummaryRepository
{
    private readonly EnterpriseAiDbContext _dbContext;

    public EfIndexingSummaryRepository(EnterpriseAiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IndexingSummaryData> GetAsync(CancellationToken cancellationToken)
    {
        var documentCounts = await _dbContext.Documents
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(group => new DocumentCounts(
                Total: group.Count(),
                Indexed: group.Count(document =>
                    document.ProcessingStatus ==
                    DocumentProcessingStatus.Indexed),
                Processing: group.Count(document =>
                    document.ProcessingStatus ==
                    DocumentProcessingStatus.Processing),
                Failed: group.Count(document =>
                    document.ProcessingStatus ==
                    DocumentProcessingStatus.Failed),
                RequiresOcr: group.Count(document =>
                    document.ProcessingStatus ==
                    DocumentProcessingStatus.RequiresOcr),
                Deleted: group.Count(document =>
                    document.IsDeleted)))
            .SingleOrDefaultAsync(cancellationToken);

        var totalChunkCount = await _dbContext.DocumentChunks
            .AsNoTracking()
            .CountAsync(cancellationToken);

        var latestRunProjection = await _dbContext.IndexingRuns
            .AsNoTracking()
            .OrderByDescending(run => run.StartedAt)
            .Select(run => new
            {
                run.Id,
                run.Status,
                run.StartedAt,
                run.CompletedAt,
                run.ProcessedCount,
                run.IndexedCount,
                run.SkippedUnchangedCount,
                run.FailedCount
            })
            .FirstOrDefaultAsync(cancellationToken);

        LatestIndexingRunData? latestRun = null;

        if (latestRunProjection is not null)
        {
            latestRun = new LatestIndexingRunData(
                Id: latestRunProjection.Id,
                Status: latestRunProjection.Status.ToString(),
                StartedAt: latestRunProjection.StartedAt,
                CompletedAt: latestRunProjection.CompletedAt,
                ProcessedCount: latestRunProjection.ProcessedCount,
                IndexedCount: latestRunProjection.IndexedCount,
                SkippedUnchangedCount: latestRunProjection.SkippedUnchangedCount,
                FailedCount: latestRunProjection.FailedCount);
        }

        return new IndexingSummaryData(
            TotalDocumentCount: documentCounts?.Total ?? 0,
            IndexedDocumentCount: documentCounts?.Indexed ?? 0,
            ProcessingDocumentCount: documentCounts?.Processing ?? 0,
            FailedDocumentCount: documentCounts?.Failed ?? 0,
            RequiresOcrDocumentCount: documentCounts?.RequiresOcr ?? 0,
            DeletedDocumentCount: documentCounts?.Deleted ?? 0,
            TotalChunkCount: totalChunkCount,
            LatestRun: latestRun);
    }

    private sealed record DocumentCounts(
        int Total,
        int Indexed,
        int Processing,
        int Failed,
        int RequiresOcr,
        int Deleted);
}
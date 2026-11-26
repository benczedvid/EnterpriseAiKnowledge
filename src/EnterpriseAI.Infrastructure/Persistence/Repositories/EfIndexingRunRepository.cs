using EnterpriseAI.Application.Abstractions.Persistence;
using EnterpriseAI.Domain.Indexing;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAI.Infrastructure.Persistence.Repositories;

public sealed class EfIndexingRunRepository : IIndexingRunRepository
{
    private const int MaximumResultLimit = 100;

    private readonly EnterpriseAiDbContext _dbContext;

    public EfIndexingRunRepository(EnterpriseAiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(IndexingRun indexingRun, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(indexingRun);

        await _dbContext.IndexingRuns.AddAsync(indexingRun, cancellationToken);
    }

    public Task<IndexingRun?> GetByIdAsync(Guid indexingRunId, CancellationToken cancellationToken)
    {
        if (indexingRunId == Guid.Empty)
        {
            throw new ArgumentException("Indexing run ID must not be empty.", nameof(indexingRunId));
        }

        return _dbContext.IndexingRuns
            .AsNoTracking()
            .Include(run => run.Items)
            .SingleOrDefaultAsync(
                run => run.Id == indexingRunId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<IndexingRun>> GetLatestAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        if (limit is < 1 or > MaximumResultLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limit),
                $"Limit must be between 1 and {MaximumResultLimit}.");
        }

        return await _dbContext.IndexingRuns
            .AsNoTracking()
            .OrderByDescending(run => run.StartedAt)
            .Take(limit)
            .ToArrayAsync(cancellationToken);
    }

    public async Task AddItemsAsync(IReadOnlyCollection<IndexingRunItem> items, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (items.Count == 0)
        {
            return;
        }

        await _dbContext.IndexingRunItems.AddRangeAsync(items, cancellationToken);
    }

    public Task<IndexingRun?> GetForProcessingAsync(Guid indexingRunId, CancellationToken cancellationToken)
    {
        if (indexingRunId == Guid.Empty)
        {
            throw new ArgumentException("Indexing run ID must not be empty.", nameof(indexingRunId));
        }

        return _dbContext.IndexingRuns
            .Include(run => run.Items)
            .SingleOrDefaultAsync(
                run => run.Id == indexingRunId,
                cancellationToken);
    }
}
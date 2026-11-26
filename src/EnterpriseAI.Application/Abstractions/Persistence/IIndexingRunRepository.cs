using EnterpriseAI.Domain.Indexing;

namespace EnterpriseAI.Application.Abstractions.Persistence;

public interface IIndexingRunRepository
{
    Task AddAsync(
        IndexingRun indexingRun,
        CancellationToken cancellationToken);

    Task AddItemsAsync(
        IReadOnlyCollection<IndexingRunItem> items,
        CancellationToken cancellationToken);

    Task<IndexingRun?> GetByIdAsync(
        Guid indexingRunId,
        CancellationToken cancellationToken);

    Task<IndexingRun?> GetForProcessingAsync(
        Guid indexingRunId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<IndexingRun>> GetLatestAsync(
        int limit,
        CancellationToken cancellationToken);
}
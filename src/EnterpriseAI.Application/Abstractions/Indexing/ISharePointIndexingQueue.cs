namespace EnterpriseAI.Application.Abstractions.Indexing;

public interface ISharePointIndexingQueue
{
    ValueTask EnqueueAsync(
        SharePointIndexingJob job,
        CancellationToken cancellationToken);

    ValueTask<SharePointIndexingJob> DequeueAsync(
        CancellationToken cancellationToken);
}
using EnterpriseAI.Application.Abstractions.Indexing;
using EnterpriseAI.Application.Abstractions.Persistence;
using EnterpriseAI.Domain.Indexing;

namespace EnterpriseAI.Application.Documents.IndexSharePointBatch;

public sealed class StartSharePointBatchHandler
{
    private const string SourceTypeName = "SharePoint";

    private readonly IIndexingRunRepository _runRepository;
    private readonly ISharePointIndexingQueue _queue;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IndexingOptions _options;

    public StartSharePointBatchHandler(
        IIndexingRunRepository runRepository,
        ISharePointIndexingQueue queue,
        IUnitOfWork unitOfWork,
        Microsoft.Extensions.Options.IOptions<IndexingOptions> options)
    {
        _runRepository = runRepository;
        _queue = queue;
        _unitOfWork = unitOfWork;
        _options = options.Value;
    }

    public async Task<Guid> HandleAsync(IndexSharePointBatchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.MaximumDocuments is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(request.MaximumDocuments));
        }

        if (request.MaximumDocuments > _options.MaximumBatchSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.MaximumDocuments),
                $"Maximum documents must not exceed {_options.MaximumBatchSize}.");
        }

        var folderPath = string.IsNullOrWhiteSpace(request.FolderPath)
                ? null
                : request.FolderPath.Trim();

        var run = new IndexingRun(
            SourceTypeName,
            folderPath,
            request.MaximumDocuments,
            request.ForceReindex);

        await _runRepository.AddAsync(run, cancellationToken);
        await _unitOfWork.SaveChangesAsync( cancellationToken);
        try
        {
            await _queue.EnqueueAsync(
                new SharePointIndexingJob(
                    IndexingRunId: run.Id,
                    FolderPath: folderPath,
                    MaximumDocuments: request.MaximumDocuments,
                    ForceReindex: request.ForceReindex),
                cancellationToken);
        }
        catch
        {
            run.MarkAsFailed("The indexing job could not be queued.");

            await _unitOfWork.SaveChangesAsync(CancellationToken.None);

            throw;
        }

        return run.Id;
    }
}
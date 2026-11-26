using EnterpriseAI.Application.Abstractions.Documents;
using EnterpriseAI.Application.Abstractions.Indexing;
using EnterpriseAI.Application.Abstractions.Persistence;
using EnterpriseAI.Application.Documents.IndexSharePointDocument;
using EnterpriseAI.Domain.Indexing;
using Microsoft.Extensions.Options;

namespace EnterpriseAI.Application.Documents.IndexSharePointBatch;

public sealed class IndexSharePointBatchHandler
{
    private const string SourceTypeName = "SharePoint";

    private static readonly string[] SupportedExtensions =
    [
        ".pdf",
        ".docx",
        ".xlsx"
    ];

    private readonly IDocumentSource _documentSource;
    private readonly ISharePointDocumentIndexer _documentIndexer;
    private readonly IIndexingRunRepository _indexingRunRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IndexingOptions _options;

    public IndexSharePointBatchHandler(
        IDocumentSource documentSource,
        ISharePointDocumentIndexer documentIndexer,
        IIndexingRunRepository indexingRunRepository,
        IUnitOfWork unitOfWork,
        IOptions<IndexingOptions> options)
    {
        ArgumentNullException.ThrowIfNull(documentSource);
        ArgumentNullException.ThrowIfNull(documentIndexer);
        ArgumentNullException.ThrowIfNull(indexingRunRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(options);

        _documentSource = documentSource;
        _documentIndexer = documentIndexer;
        _indexingRunRepository = indexingRunRepository;
        _unitOfWork = unitOfWork;
        _options = options.Value;
    }

    public async Task<IndexSharePointBatchResponse> HandleAsync(
        IndexSharePointBatchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        ValidateMaximumDocuments(request.MaximumDocuments);

        var normalizedFolderPath = NormalizeFolderPath(request.FolderPath);

        var indexingRun = new IndexingRun(
            sourceType: SourceTypeName,
            folderPath: normalizedFolderPath,
            requestedMaximumDocuments: request.MaximumDocuments,
            forceReindex: request.ForceReindex);

        await _indexingRunRepository.AddAsync(indexingRun, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            var sourceDocuments =
                await _documentSource.DiscoverAsync(
                    new DocumentSourceQuery(
                        FolderPath: normalizedFolderPath,
                        AllowedExtensions: SupportedExtensions,
                        MaximumDocuments: request.MaximumDocuments),
                    cancellationToken);

            var items = new List<IndexSharePointBatchItemResponse>(sourceDocuments.Count);

            foreach (var sourceDocument in sourceDocuments)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var item = await IndexDocumentSafelyAsync(
                        sourceDocument,
                        request.ForceReindex,
                        cancellationToken);

                items.Add(item);

                indexingRun.AddItem(
                    sourceId: item.SourceId,
                    fileName: item.FileName,
                    status: MapItemStatus(item.Status),
                    message: item.Message,
                    documentId: item.DocumentId,
                    chunkCount: item.ChunkCount,
                    characterCount: item.CharacterCount,
                    indexedAt: item.IndexedAt);

                var persistedItem = indexingRun.Items.Last();

                await _indexingRunRepository.AddItemsAsync([persistedItem], cancellationToken);

                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            indexingRun.Complete(discoveredCount: sourceDocuments.Count);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var completedAt = indexingRun.CompletedAt ?? throw new InvalidOperationException(
                    "Completed indexing run has no completion timestamp.");

            return new IndexSharePointBatchResponse(
                IndexingRunId: indexingRun.Id,
                DiscoveredCount: indexingRun.DiscoveredCount,
                ProcessedCount: indexingRun.ProcessedCount,
                IndexedCount: indexingRun.IndexedCount,
                SkippedUnchangedCount: indexingRun.SkippedUnchangedCount,
                SkippedUnsupportedCount: indexingRun.SkippedUnsupportedCount,
                RequiresOcrCount: indexingRun.RequiresOcrCount,
                FailedCount: indexingRun.FailedCount,
                NotFoundCount: indexingRun.NotFoundCount,
                TotalChunkCount: indexingRun.TotalChunkCount,
                StartedAt: indexingRun.StartedAt,
                CompletedAt: completedAt,
                Items: items);
        }
        catch (OperationCanceledException)
        {
            await MarkRunAsCancelledSafelyAsync(indexingRun);

            throw;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            await MarkRunAsFailedSafelyAsync(indexingRun, exception);

            throw;
        }
    }

    private async Task<IndexSharePointBatchItemResponse>IndexDocumentSafelyAsync(
            SourceDocument sourceDocument,
            bool forceReindex,
            CancellationToken cancellationToken)
    {
        try
        {
            var response = await _documentIndexer.IndexAsync(
                    new IndexSharePointDocumentRequest(
                        SourceId: sourceDocument.SourceId,
                        ForceReindex: forceReindex),
                    cancellationToken);

            return MapResponse(response);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return new IndexSharePointBatchItemResponse(
                SourceId: sourceDocument.SourceId,
                FileName: sourceDocument.FileName,
                Status: IndexSharePointDocumentStatus.Failed,
                Message: CreateSafeItemFailureMessage(exception),
                DocumentId: null,
                ChunkCount: 0,
                CharacterCount: 0,
                IndexedAt: null);
        }
    }

    private async Task MarkRunAsCancelledSafelyAsync(
        IndexingRun indexingRun)
    {
        if (indexingRun.Status != IndexingRunStatus.Running)
        {
            return;
        }

        try
        {
            indexingRun.MarkAsCancelled();

            await _unitOfWork.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {

        }
    }

    private async Task MarkRunAsFailedSafelyAsync(IndexingRun indexingRun, Exception exception)
    {
        if (indexingRun.Status != IndexingRunStatus.Running)
        {
            return;
        }

        try
        {
            indexingRun.MarkAsFailed(CreateSafeRunFailureMessage(exception));

            await _unitOfWork.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception persistenceException) when (persistenceException is not OutOfMemoryException)
        {
        }
    }

    private static IndexSharePointBatchItemResponse MapResponse(IndexSharePointDocumentResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return new IndexSharePointBatchItemResponse(
            SourceId: response.SourceId,
            FileName: response.FileName,
            Status: response.Status,
            Message: response.Message,
            DocumentId: response.DocumentId,
            ChunkCount: response.ChunkCount,
            CharacterCount: response.CharacterCount,
            IndexedAt: response.IndexedAt);
    }

    private void ValidateMaximumDocuments(int maximumDocuments)
    {
        if (maximumDocuments < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumDocuments),
                "Maximum documents must be greater than zero.");
        }

        if (maximumDocuments > _options.MaximumBatchSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumDocuments),
                $"Maximum documents must not exceed {_options.MaximumBatchSize}.");
        }
    }

    private static string? NormalizeFolderPath(string? folderPath)
    {
        return string.IsNullOrWhiteSpace(folderPath)
            ? null
            : folderPath.Trim();
    }

    private static string CreateSafeItemFailureMessage(Exception exception)
    {
        return exception switch
        {
            HttpRequestException => "An external HTTP service request failed.",

            InvalidOperationException => "The document could not be processed.",

            _ => "An unexpected document indexing error occurred."
        };
    }

    private static string CreateSafeRunFailureMessage(Exception exception)
    {
        return exception switch
        {
            HttpRequestException => "An external HTTP service request failed.",

            InvalidOperationException => "The SharePoint batch indexing run could not be completed.",

            _ => "The SharePoint batch indexing run failed."
        };
    }

    private static IndexingRunItemStatus MapItemStatus(IndexSharePointDocumentStatus status)
    {
        return status switch
        {
            IndexSharePointDocumentStatus.Indexed => IndexingRunItemStatus.Indexed,
            IndexSharePointDocumentStatus.SkippedUnchanged => IndexingRunItemStatus.SkippedUnchanged,
            IndexSharePointDocumentStatus.SkippedUnsupported => IndexingRunItemStatus.SkippedUnsupported,
            IndexSharePointDocumentStatus.RequiresOcr => IndexingRunItemStatus.RequiresOcr,
            IndexSharePointDocumentStatus.Failed => IndexingRunItemStatus.Failed,
            IndexSharePointDocumentStatus.NotFound => IndexingRunItemStatus.NotFound,

            _ => throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "Unknown document indexing status.")
        };
    }
}
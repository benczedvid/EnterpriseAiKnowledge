using EnterpriseAI.Application.Abstractions.Documents;
using EnterpriseAI.Application.Abstractions.Indexing;
using EnterpriseAI.Application.Abstractions.Persistence;
using EnterpriseAI.Application.Documents.IndexSharePointDocument;
using EnterpriseAI.Domain.Indexing;

namespace EnterpriseAI.Application.Documents.IndexSharePointBatch;

public sealed class ProcessSharePointIndexingJobHandler
{
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

    public ProcessSharePointIndexingJobHandler(
        IDocumentSource documentSource,
        ISharePointDocumentIndexer documentIndexer,
        IIndexingRunRepository indexingRunRepository,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(documentSource);
        ArgumentNullException.ThrowIfNull(documentIndexer);
        ArgumentNullException.ThrowIfNull(indexingRunRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _documentSource = documentSource;
        _documentIndexer = documentIndexer;
        _indexingRunRepository = indexingRunRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task HandleAsync(SharePointIndexingJob job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);

        var indexingRun = await _indexingRunRepository
                .GetForProcessingAsync(
                    job.IndexingRunId,
                    cancellationToken)
            ?? throw new InvalidOperationException($"Indexing run '{job.IndexingRunId}' was not found.");

        /*
         * Ugyanaz a job ne legyen ismét feldolgozható,
         * ha a futás már lezárult.
         */
        if (indexingRun.Status != IndexingRunStatus.Running)
        {
            return;
        }

        try
        {
            var sourceDocuments = await _documentSource.DiscoverAsync(
                    new DocumentSourceQuery(
                        FolderPath: job.FolderPath,
                        AllowedExtensions: SupportedExtensions,
                        MaximumDocuments: job.MaximumDocuments),
                    cancellationToken);

            /*
             * Újraindítás vagy duplikált queue-job esetén
             * a már elmentett itemeket kihagyjuk.
             */
            var processedSourceIds = indexingRun.Items
                .Select(item => item.SourceId)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var sourceDocument in sourceDocuments)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (processedSourceIds.Contains(sourceDocument.SourceId))
                {
                    continue;
                }

                var item = await IndexDocumentSafelyAsync(
                        sourceDocument,
                        job.ForceReindex,
                        cancellationToken);

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

                /*
                 * Dokumentumonként mentjük az előrehaladást.
                 * API- vagy böngészőkapcsolat nincs a worker
                 * cancellation tokenjéhez kötve.
                 */
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                processedSourceIds.Add(sourceDocument.SourceId);
            }

            indexingRun.Complete(discoveredCount: sourceDocuments.Count);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            /*
             * Ez csak az alkalmazás leállításakor fut.
             * A weboldal újratöltése nem érinti ezt a tokent.
             */
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            /*
             * Az alkalmazás valóban leáll.
             * A teljes worker-feldolgozás megszakad.
             */
            throw;
        }
        catch (OperationCanceledException)
        {
            /*
             * Egy külső szolgáltatás időtúllépése vagy
             * megszakítása csak az aktuális dokumentumot
             * teszi hibássá.
             */
            return CreateFailedItem(
                sourceDocument,
                "The document processing operation was cancelled by an external service.");
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return CreateFailedItem(sourceDocument, CreateSafeItemFailureMessage(exception));
        }
    }

    private async Task MarkRunAsCancelledSafelyAsync(IndexingRun indexingRun)
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
            /*
             * A lezárás hibáját a BackgroundService naplózza
             * az eredeti worker-kivétellel együtt.
             */
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
            /*
             * A másodlagos perzisztenciahiba nem fedheti el
             * az eredeti jobhibát.
             */
        }
    }

    private static IndexSharePointBatchItemResponse
        CreateFailedItem(SourceDocument sourceDocument, string message)
    {
        return new IndexSharePointBatchItemResponse(
            SourceId: sourceDocument.SourceId,
            FileName: sourceDocument.FileName,
            Status: IndexSharePointDocumentStatus.Failed,
            Message: message,
            DocumentId: null,
            ChunkCount: 0,
            CharacterCount: 0,
            IndexedAt: null);
    }

    private static IndexSharePointBatchItemResponse MapResponse(IndexSharePointDocumentResponse response)
    {
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

            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown document indexing status.")
        };
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
}
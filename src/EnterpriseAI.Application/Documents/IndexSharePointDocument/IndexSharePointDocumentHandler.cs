using EnterpriseAI.Application.Abstractions.AI;
using EnterpriseAI.Application.Abstractions.Chunking;
using EnterpriseAI.Application.Abstractions.Documents;
using EnterpriseAI.Application.Abstractions.Parsing;
using EnterpriseAI.Application.Abstractions.Persistence;
using EnterpriseAI.Domain.Documents;
using KnowledgeDocument =
    EnterpriseAI.Domain.Documents.Document;

namespace EnterpriseAI.Application.Documents.IndexSharePointDocument;

public sealed class IndexSharePointDocumentHandler
{
    private const string SourceTypeName = "SharePoint";

    private readonly IDocumentSource _documentSource;
    private readonly IDocumentParserResolver _parserResolver;
    private readonly IChunkingService _chunkingService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IDocumentRepository _documentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public IndexSharePointDocumentHandler(
        IDocumentSource documentSource,
        IDocumentParserResolver parserResolver,
        IChunkingService chunkingService,
        IEmbeddingService embeddingService,
        IDocumentRepository documentRepository,
        IUnitOfWork unitOfWork)
    {
        _documentSource = documentSource;
        _parserResolver = parserResolver;
        _chunkingService = chunkingService;
        _embeddingService = embeddingService;
        _documentRepository = documentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IndexSharePointDocumentResponse> HandleAsync(
        IndexSharePointDocumentRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sourceId = GetRequiredSourceId(request.SourceId);
        var sourceDocument = await FindSourceDocumentAsync(sourceId, cancellationToken);

        if (sourceDocument is null)
        {
            return CreateResponse(
                sourceId: sourceId,
                fileName: null,
                status: IndexSharePointDocumentStatus.NotFound,
                message: "The requested SharePoint document was not found.",
                document: null,
                chunkCount: 0,
                characterCount: 0);
        }

        var document = await _documentRepository.GetBySourceAsync(
                sourceType: SourceTypeName,
                sourceId: sourceDocument.SourceId,
                includeChunks: true,
                cancellationToken);

        if (document is not null
            && !request.ForceReindex
            && !document.HasSourceChanged(
                sourceDocument.ETag,
                sourceDocument.SourceModifiedAt))
        {
            return CreateResponse(
                sourceId: sourceDocument.SourceId,
                fileName: sourceDocument.FileName,
                status: IndexSharePointDocumentStatus.SkippedUnchanged,
                message:"The SharePoint document has not changed.",
                document: document,
                chunkCount: 0,
                characterCount: 0);
        }

        document = await PrepareDocumentAsync(sourceDocument, document, cancellationToken);

        var parser = _parserResolver.Resolve(sourceDocument);

        if (parser is null)
        {
            document.MarkAsSkipped("No parser is registered for this file type.");

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return CreateResponse(
                sourceId: sourceDocument.SourceId,
                fileName: sourceDocument.FileName,
                status: IndexSharePointDocumentStatus.SkippedUnsupported,
                message:"No parser is registered for this file type.", document: document,
                chunkCount: 0,
                characterCount: 0);
        }

        document.StartProcessing();

        try
        {
            await using var content = await _documentSource.DownloadAsync(sourceDocument.SourceId, cancellationToken);

            var parseResult = await parser.ParseAsync(
                new DocumentParseRequest(
                    SourceDocument: sourceDocument,
                    Content: content),
                cancellationToken);

            return await HandleParseResultAsync(
                sourceDocument,
                document,
                parseResult,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            if (document.ProcessingStatus != DocumentProcessingStatus.Processing)
            {
                throw;
            }

            document.MarkAsFailed(GetSafeFailureReason(exception));

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return CreateResponse(
                sourceId: sourceDocument.SourceId,
                fileName: sourceDocument.FileName,
                status: IndexSharePointDocumentStatus.Failed,
                message: "The SharePoint document could not be indexed.",
                document: document,
                chunkCount: 0,
                characterCount: 0);
        }
    }

    private Task<SourceDocument?> FindSourceDocumentAsync(string sourceId, CancellationToken cancellationToken)
    {
        return _documentSource.GetByIdAsync(sourceId, cancellationToken);
    }

    private async Task<KnowledgeDocument> PrepareDocumentAsync(
        SourceDocument sourceDocument,
        KnowledgeDocument? existingDocument,
        CancellationToken cancellationToken)
    {
        if (existingDocument is null)
        {
            var newDocument = new KnowledgeDocument(
                sourceId: sourceDocument.SourceId,
                sourceUrl: sourceDocument.SourceUrl,
                fileName: sourceDocument.FileName,
                title: sourceDocument.Title,
                mimeType: sourceDocument.MimeType,
                folderPath: sourceDocument.FolderPath,
                etag: sourceDocument.ETag,
                sourceCreatedAt: sourceDocument.SourceCreatedAt,
                sourceModifiedAt: sourceDocument.SourceModifiedAt);

            await _documentRepository.AddAsync(newDocument, cancellationToken);

            return newDocument;
        }

        existingDocument.UpdateSourceMetadata(
            sourceUrl: sourceDocument.SourceUrl,
            fileName: sourceDocument.FileName,
            title: sourceDocument.Title,
            mimeType: sourceDocument.MimeType,
            folderPath: sourceDocument.FolderPath,
            etag: sourceDocument.ETag,
            sourceCreatedAt: sourceDocument.SourceCreatedAt,
            sourceModifiedAt: sourceDocument.SourceModifiedAt);

        return existingDocument;
    }

    private async Task<IndexSharePointDocumentResponse>
        HandleParseResultAsync(
            SourceDocument sourceDocument,
            KnowledgeDocument document,
            DocumentParseResult parseResult,
            CancellationToken cancellationToken)
    {
        switch (parseResult.Status)
        {
            case DocumentParseStatus.RequiresOcr:
                document.MarkAsRequiresOcr();

                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return CreateResponse(
                    sourceId: sourceDocument.SourceId,
                    fileName: sourceDocument.FileName,
                    status: IndexSharePointDocumentStatus.RequiresOcr,
                    message: parseResult.Message ?? "OCR processing is required.",
                    document: document,
                    chunkCount: 0,
                    characterCount: 0);

            case DocumentParseStatus.Unsupported:
                document.MarkAsSkipped(parseResult.Message);

                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return CreateResponse(
                    sourceId: sourceDocument.SourceId,
                    fileName: sourceDocument.FileName,
                    status: IndexSharePointDocumentStatus.SkippedUnsupported,
                    message: parseResult.Message ?? "The document format is unsupported.",
                    document: document,
                    chunkCount: 0,
                    characterCount: 0);

            case DocumentParseStatus.Success:
                break;

            default:
                throw new InvalidOperationException($"Unknown parse status: {parseResult.Status}.");
        }

        var parsedDocument =
            parseResult.Document
            ?? throw new InvalidOperationException(
                "A successful parser result must contain a parsed document.");

        var chunkingResult = _chunkingService.CreateChunks(new ChunkingRequest(parsedDocument));

        var embeddingInputs = chunkingResult.Chunks
            .Select(chunk =>
                CreateEmbeddingInput(
                    sourceDocument,
                    chunk))
            .ToArray();

        var embeddingResponse = await _embeddingService.CreateEmbeddingsAsync(
                new EmbeddingRequest(embeddingInputs),
                cancellationToken);

        if (embeddingResponse.Embeddings.Count != chunkingResult.Chunks.Count)
        {
            throw new InvalidOperationException("The embedding count does not match the text chunk count.");
        }

        var documentChunks = CreateDocumentChunks(
            document.Id,
            chunkingResult,
            embeddingResponse);

        var previousChunks = document.Chunks.ToArray();

        _documentRepository.RemoveChunks(previousChunks);

        document.ReplaceChunks(documentChunks);

        await _documentRepository.AddChunksAsync(documentChunks, cancellationToken);

        document.MarkAsIndexed();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return CreateResponse(
            sourceId: sourceDocument.SourceId,
            fileName: sourceDocument.FileName,
            status: IndexSharePointDocumentStatus.Indexed,
            message: "The SharePoint document was indexed successfully.",
            document: document,
            chunkCount: documentChunks.Length,
            characterCount: parsedDocument.CharacterCount);
    }

    private static DocumentChunk[] CreateDocumentChunks(
        Guid documentId,
        ChunkingResult chunkingResult,
        EmbeddingResponse embeddingResponse)
    {
        var documentChunks = new DocumentChunk[chunkingResult.Chunks.Count];

        for (var index = 0; index < chunkingResult.Chunks.Count; index++)
        {
            var textChunk = chunkingResult.Chunks[index];

            documentChunks[index] =
                new DocumentChunk(
                    documentId: documentId,
                    content: textChunk.Content,
                    sectionTitle: textChunk.SectionTitle,
                    pageNumber: textChunk.PageNumber,
                    chunkIndex: textChunk.ChunkIndex,
                    tokenCount: textChunk.EstimatedTokenCount,
                    embedding: embeddingResponse.Embeddings[index]);
        }

        return documentChunks;
    }

    private static IndexSharePointDocumentResponse
        CreateResponse(
            string sourceId,
            string? fileName,
            IndexSharePointDocumentStatus status,
            string message,
            KnowledgeDocument? document,
            int chunkCount,
            int characterCount)
    {
        return new IndexSharePointDocumentResponse(
            SourceId: sourceId,
            FileName: fileName,
            Status: status,
            Message: message,
            DocumentId: document?.Id,
            ChunkCount: chunkCount,
            CharacterCount: characterCount,
            IndexedAt: document?.IndexedAt);
    }

    private static string GetRequiredSourceId(string sourceId)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            throw new ArgumentException("Source ID must not be empty.", nameof(sourceId));
        }

        return sourceId.Trim();
    }

    private static string GetSafeFailureReason(
        Exception exception)
    {
        var message = exception.Message.Trim();

        if (string.IsNullOrWhiteSpace(message))
        {
            return "An unspecified indexing error occurred.";
        }

        return message.Length <= 2000 ? message : message[..2000];
    }

    private static string CreateEmbeddingInput(SourceDocument sourceDocument, TextChunk chunk)
    {
        var parts = new List<string>
        {
            $"Dokumentum: {sourceDocument.Title}"
        };

        if (!string.IsNullOrWhiteSpace(chunk.SectionTitle))
        {
            parts.Add($"Fejezet: {chunk.SectionTitle}");
        }

        if (chunk.PageNumber.HasValue)
        {
            parts.Add($"Oldal: {chunk.PageNumber.Value}");
        }

        parts.Add($"Tartalom: {chunk.Content}");

        return string.Join(
            Environment.NewLine,
            parts);
    }
}
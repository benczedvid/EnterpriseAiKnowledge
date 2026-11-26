using EnterpriseAI.Application.Abstractions.Persistence;
using EnterpriseAI.Domain.Documents;

namespace EnterpriseAI.Application.Documents.GetBySource;

public sealed class GetDocumentBySourceHandler
{
    private readonly IDocumentRepository _documentRepository;

    public GetDocumentBySourceHandler(IDocumentRepository documentRepository)
    {
        _documentRepository = documentRepository;
    }

    public async Task<GetDocumentBySourceResponse> HandleAsync(GetDocumentBySourceRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sourceType = GetRequiredValue(request.SourceType, nameof(request.SourceType));
        var sourceId = GetRequiredValue(request.SourceId, nameof(request.SourceId));
        var document = await _documentRepository.GetBySourceAsync(
            sourceType,
            sourceId,
            request.IncludeChunks,
            cancellationToken);

        if (document is null)
        {
            return GetDocumentBySourceResponse.NotFound();
        }

        return GetDocumentBySourceResponse.Success(MapDocument(document, request.IncludeChunks));
    }

    private static DocumentDetailsResponse MapDocument(Document document, bool includeChunks)
    {
        var chunks = includeChunks
            ? document.Chunks
                .OrderBy(chunk => chunk.ChunkIndex)
                .Select(MapChunk)
                .ToArray()
            : [];

        return new DocumentDetailsResponse(
            Id: document.Id,
            SourceType: document.SourceType,
            SourceId: document.SourceId,
            SourceUrl: document.SourceUrl,
            FileName: document.FileName,
            Title: document.Title,
            MimeType: document.MimeType,
            FolderPath: document.FolderPath,
            ETag: document.ETag,
            SourceCreatedAt: document.SourceCreatedAt,
            SourceModifiedAt: document.SourceModifiedAt,
            IndexedAt: document.IndexedAt,
            ProcessingStatus: document.ProcessingStatus.ToString(),
            FailureReason: document.FailureReason,
            IsDeleted: document.IsDeleted,
            ChunkCount: document.Chunks.Count,
            Chunks: chunks);
    }

    private static DocumentChunkResponse MapChunk(DocumentChunk chunk)
    {
        return new DocumentChunkResponse(
            Id: chunk.Id,
            Content: chunk.Content,
            SectionTitle: chunk.SectionTitle,
            PageNumber: chunk.PageNumber,
            ChunkIndex: chunk.ChunkIndex,
            TokenCount: chunk.TokenCount);
    }

    private static string GetRequiredValue(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be empty.", parameterName);
        }

        return value.Trim();
    }
}
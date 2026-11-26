namespace EnterpriseAI.Application.Documents.GetBySource;

public sealed record GetDocumentBySourceResponse(
    bool Found,
    DocumentDetailsResponse? Document)
{
    public static GetDocumentBySourceResponse NotFound()
    {
        return new GetDocumentBySourceResponse(
            Found: false,
            Document: null);
    }

    public static GetDocumentBySourceResponse Success(DocumentDetailsResponse document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return new GetDocumentBySourceResponse(
            Found: true,
            Document: document);
    }
}

public sealed record DocumentDetailsResponse(
    Guid Id,
    string SourceType,
    string SourceId,
    string SourceUrl,
    string FileName,
    string Title,
    string MimeType,
    string? FolderPath,
    string? ETag,
    DateTimeOffset SourceCreatedAt,
    DateTimeOffset SourceModifiedAt,
    DateTimeOffset? IndexedAt,
    string ProcessingStatus,
    string? FailureReason,
    bool IsDeleted,
    int ChunkCount,
    IReadOnlyList<DocumentChunkResponse> Chunks);

public sealed record DocumentChunkResponse(
    Guid Id,
    string Content,
    string? SectionTitle,
    int? PageNumber,
    int ChunkIndex,
    int TokenCount);
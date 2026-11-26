namespace EnterpriseAI.Application.Documents.IndexSharePointDocument;

public sealed record IndexSharePointDocumentResponse(
    string SourceId,
    string? FileName,
    IndexSharePointDocumentStatus Status,
    string Message,
    Guid? DocumentId,
    int ChunkCount,
    int CharacterCount,
    DateTimeOffset? IndexedAt)
{
    public bool Succeeded => Status == IndexSharePointDocumentStatus.Indexed;
}
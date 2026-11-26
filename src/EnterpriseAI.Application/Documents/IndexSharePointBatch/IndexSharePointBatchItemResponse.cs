using EnterpriseAI.Application.Documents.IndexSharePointDocument;

namespace EnterpriseAI.Application.Documents.IndexSharePointBatch;

public sealed record IndexSharePointBatchItemResponse(
    string SourceId,
    string? FileName,
    IndexSharePointDocumentStatus Status,
    string Message,
    Guid? DocumentId,
    int ChunkCount,
    int CharacterCount,
    DateTimeOffset? IndexedAt);
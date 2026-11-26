namespace EnterpriseAI.Application.Documents.GetBySource;

public sealed record GetDocumentBySourceRequest(
    string SourceType,
    string SourceId,
    bool IncludeChunks = false);
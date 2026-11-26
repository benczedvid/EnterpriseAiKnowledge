namespace EnterpriseAI.Application.Documents.IndexSharePointDocument;

public sealed record IndexSharePointDocumentRequest(
    string SourceId,
    bool ForceReindex = false);
namespace EnterpriseAI.Application.Documents.IndexSharePointBatch;

public sealed record IndexSharePointBatchRequest(
    string? FolderPath = null,
    int MaximumDocuments = 20,
    bool ForceReindex = false);
namespace EnterpriseAI.Application.Abstractions.Indexing;

public sealed record SharePointIndexingJob(
    Guid IndexingRunId,
    string? FolderPath,
    int MaximumDocuments,
    bool ForceReindex);
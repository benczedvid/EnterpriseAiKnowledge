namespace EnterpriseAI.Application.Abstractions.Documents;

public sealed record DocumentSourceQuery(
    string? FolderPath = null,
    IReadOnlyCollection<string>? AllowedExtensions = null,
    int? MaximumDocuments = null);
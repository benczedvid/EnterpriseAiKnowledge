namespace EnterpriseAI.Application.Abstractions.Documents;

public sealed record SourceDocument(
    string SourceId,
    string SourceType,
    string SourceUrl,
    string FileName,
    string Title,
    string MimeType,
    string? FolderPath,
    string? ETag,
    long SizeInBytes,
    DateTimeOffset SourceCreatedAt,
    DateTimeOffset SourceModifiedAt);
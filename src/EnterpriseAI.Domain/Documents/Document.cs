namespace EnterpriseAI.Domain.Documents;

public sealed class Document
{
    private const int MaximumFailureReasonLength = 2000;
    private readonly List<DocumentChunk> _chunks = [];

    private Document()
    {
    }

    public Document(
        string sourceId,
        string sourceUrl,
        string fileName,
        string title,
        string mimeType,
        string? folderPath,
        string? etag,
        DateTimeOffset sourceCreatedAt,
        DateTimeOffset sourceModifiedAt)
    {
        Id = Guid.NewGuid();
        SourceId = GetRequiredValue(sourceId, nameof(sourceId));
        SourceUrl = GetRequiredValue(sourceUrl, nameof(sourceUrl));
        FileName = GetRequiredValue(fileName, nameof(fileName));
        Title = GetRequiredValue(title, nameof(title));
        MimeType = GetRequiredValue(mimeType, nameof(mimeType));
        FolderPath = NormalizeOptionalValue(folderPath);
        ETag = NormalizeOptionalValue(etag);
        SourceCreatedAt = sourceCreatedAt;
        SourceModifiedAt = sourceModifiedAt;
        ProcessingStatus = DocumentProcessingStatus.Discovered;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid Id { get; private set; }
    public string SourceType { get; private set; } = "SharePoint";
    public string SourceId { get; private set; } = string.Empty;
    public string SourceUrl { get; private set; } = string.Empty;
    public string FileName { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string MimeType { get; private set; }  = string.Empty;
    public string? FolderPath { get; private set; }
    public string? ETag { get; private set; }
    public DateTimeOffset SourceCreatedAt { get; private set; }
    public DateTimeOffset SourceModifiedAt { get; private set; }
    public DateTimeOffset? IndexedAt { get; private set; }
    public DocumentProcessingStatus ProcessingStatus { get; private set; }
    public string? FailureReason { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyCollection<DocumentChunk> Chunks => _chunks.AsReadOnly();


    public void ReplaceChunks(IReadOnlyCollection<DocumentChunk> chunks)
    {
        EnsureProcessing();

        ArgumentNullException.ThrowIfNull(chunks);

        if (chunks.Count == 0)
        {
            throw new ArgumentException("At least one document chunk is required.", nameof(chunks));
        }

        if (chunks.Any(chunk => chunk.DocumentId != Id))
        {
            throw new ArgumentException("Every chunk must belong to this document.", nameof(chunks));
        }

        var orderedChunks = chunks
            .OrderBy(chunk => chunk.ChunkIndex)
            .ToArray();

        for (var index = 0; index < orderedChunks.Length; index++)
        {
            if (orderedChunks[index].ChunkIndex != index)
            {
                throw new ArgumentException("Chunk indexes must be unique, continuous, and start at zero.", nameof(chunks));
            }
        }

        _chunks.Clear();
        _chunks.AddRange(orderedChunks);

        UpdatedAt = DateTimeOffset.UtcNow;
    }
    public void StartProcessing()
    {
        EnsureNotDeleted();

        if (ProcessingStatus == DocumentProcessingStatus.Processing)
        {
            throw new InvalidOperationException("The document is already being processed.");
        }

        ProcessingStatus = DocumentProcessingStatus.Processing;
        FailureReason = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkAsIndexed()
    {
        EnsureProcessing();

        if (_chunks.Count == 0)
        {
            throw new InvalidOperationException("The document cannot be marked as indexed without document chunks.");
        }

        ProcessingStatus = DocumentProcessingStatus.Indexed;
        FailureReason = null;
        IndexedAt = DateTimeOffset.UtcNow;
        UpdatedAt = IndexedAt.Value;
    }

    public void MarkAsFailed(string failureReason)
    {
        EnsureProcessing();

        FailureReason = GetFailureReason(failureReason);
        ProcessingStatus = DocumentProcessingStatus.Failed;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkAsRequiresOcr()
    {
        EnsureProcessing();

        ProcessingStatus = DocumentProcessingStatus.RequiresOcr;
        FailureReason = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkAsSkipped(string? reason = null)
    {
        EnsureNotDeleted();

        if (ProcessingStatus == DocumentProcessingStatus.Indexed)
        {
            throw new InvalidOperationException("An indexed document cannot be marked as skipped.");
        }

        ProcessingStatus = DocumentProcessingStatus.Skipped;
        FailureReason = NormalizeFailureReason(reason);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public bool HasSourceChanged(string? etag, DateTimeOffset sourceModifiedAt)
    {
        var normalizedEtag = NormalizeOptionalValue(etag);

        if (!string.Equals(ETag, normalizedEtag, StringComparison.Ordinal))
        {
            return true;
        }

        return SourceModifiedAt != sourceModifiedAt;
    }

    public void UpdateSourceMetadata(
        string sourceUrl,
        string fileName,
        string title,
        string mimeType,
        string? folderPath,
        string? etag,
        DateTimeOffset sourceCreatedAt,
        DateTimeOffset sourceModifiedAt)
    {
        EnsureNotDeleted();

        if (ProcessingStatus == DocumentProcessingStatus.Processing)
        {
            throw new InvalidOperationException(
                "Source metadata cannot be updated while the document is being processed.");
        }

        SourceUrl = GetRequiredValue(sourceUrl, nameof(sourceUrl));
        FileName = GetRequiredValue(fileName, nameof(fileName));
        Title = GetRequiredValue(title, nameof(title));
        MimeType = GetRequiredValue(mimeType, nameof(mimeType));
        FolderPath = NormalizeOptionalValue(folderPath);
        ETag = NormalizeOptionalValue(etag);
        SourceCreatedAt = sourceCreatedAt;
        SourceModifiedAt = sourceModifiedAt;
        IsDeleted = false;
        ProcessingStatus = DocumentProcessingStatus.Discovered;
        FailureReason = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkAsDeleted()
    {
        if (IsDeleted)
        {
            return;
        }

        IsDeleted = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private void EnsureProcessing()
    {
        EnsureNotDeleted();

        if (ProcessingStatus != DocumentProcessingStatus.Processing)
        {
            throw new InvalidOperationException("The document must be in Processing status.");
        }
    }

    private void EnsureNotDeleted()
    {
        if (IsDeleted)
        {
            throw new InvalidOperationException("A deleted document cannot be processed.");
        }
    }

    private static string GetRequiredValue(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be empty.", parameterName);
        }

        return value.Trim();
    }

    private static string GetFailureReason(string value)
    {
        var normalizedValue = NormalizeFailureReason(value);

        if (normalizedValue is null)
        {
            throw new ArgumentException("Failure reason must not be empty.", nameof(value));
        }

        return normalizedValue;
    }

    private static string? NormalizeFailureReason(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalizedValue = value.Trim();

        return normalizedValue.Length <= MaximumFailureReasonLength
            ? normalizedValue
            : normalizedValue[..MaximumFailureReasonLength];
    }

    private static string? NormalizeOptionalValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
namespace EnterpriseAI.Domain.Indexing;

public sealed class IndexingRunItem
{
    private const int MaximumSourceIdLength = 500;
    private const int MaximumFileNameLength = 500;
    private const int MaximumMessageLength = 2000;

    private IndexingRunItem()
    {
    }

    public IndexingRunItem(
        Guid indexingRunId,
        string sourceId,
        string? fileName,
        IndexingRunItemStatus status,
        string message,
        Guid? documentId,
        int chunkCount,
        int characterCount,
        DateTimeOffset? indexedAt)
    {
        if (indexingRunId == Guid.Empty)
        {
            throw new ArgumentException("Indexing run ID must not be empty.", nameof(indexingRunId));
        }

        if (documentId == Guid.Empty)
        {
            throw new ArgumentException("Document ID must not be empty.", nameof(documentId));
        }

        if (chunkCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(chunkCount), "Chunk count must not be negative.");
        }

        if (characterCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(characterCount), "Character count must not be negative.");
        }

        Id = Guid.NewGuid();
        IndexingRunId = indexingRunId;

        SourceId = GetRequiredValue(
            sourceId,
            MaximumSourceIdLength,
            nameof(sourceId));

        FileName = NormalizeOptionalValue(
            fileName,
            MaximumFileNameLength);

        Status = status;

        Message = GetRequiredValue(
            message,
            MaximumMessageLength,
            nameof(message));

        DocumentId = documentId;
        ChunkCount = chunkCount;
        CharacterCount = characterCount;
        IndexedAt = indexedAt;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid IndexingRunId { get; private set; }
    public string SourceId { get; private set; } = string.Empty;
    public string? FileName { get; private set; }
    public IndexingRunItemStatus Status { get; private set; }
    public string Message { get; private set; } = string.Empty;
    public Guid? DocumentId { get; private set; }
    public int ChunkCount { get; private set; }
    public int CharacterCount { get; private set; }
    public DateTimeOffset? IndexedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    private static string GetRequiredValue(
        string value,
        int maximumLength,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be empty.", parameterName);
        }

        var normalized = value.Trim();

        return normalized.Length <= maximumLength
            ? normalized
            : normalized[..maximumLength];
    }

    private static string? NormalizeOptionalValue(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();

        return normalized.Length <= maximumLength
            ? normalized
            : normalized[..maximumLength];
    }
}
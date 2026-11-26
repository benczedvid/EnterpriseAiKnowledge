namespace EnterpriseAI.Domain.Documents;

public sealed class DocumentChunk
{
    public const int EmbeddingDimensions = 1024;
    

    private DocumentChunk()
    {
    }

    public DocumentChunk(
        Guid documentId,
        string content,
        string? sectionTitle,
        int? pageNumber,
        int chunkIndex,
        int tokenCount,
        IReadOnlyList<float> embedding)
    {
        if (documentId == Guid.Empty)
        {
            throw new ArgumentException("Document ID must not be empty.", nameof(documentId));
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Content must not be empty.", nameof(content));
        }

        if (pageNumber is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumber), "Page number must be greater than zero.");
        }

        if (chunkIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(chunkIndex), "Chunk index must not be negative.");
        }

        if (tokenCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tokenCount), "Token count must not be negative.");
        }

        ArgumentNullException.ThrowIfNull(embedding);

        if (embedding.Count != EmbeddingDimensions)
        {
            throw new ArgumentException($"Embedding must contain exactly {EmbeddingDimensions} values.", nameof(embedding));
        }

        Id = Guid.NewGuid();
        DocumentId = documentId;
        Content = content.Trim();
        SectionTitle = NormalizeOptionalValue(sectionTitle);
        PageNumber = pageNumber;
        ChunkIndex = chunkIndex;
        TokenCount = tokenCount;
        Embedding = embedding.ToArray();
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid DocumentId { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public string? SectionTitle { get; private set; }
    public int? PageNumber { get; private set; }
    public int ChunkIndex { get; private set; }
    public int TokenCount { get; private set; }
    public float[] Embedding { get; private set; } = [];
    public DateTimeOffset CreatedAt { get; private set; }

    private static string? NormalizeOptionalValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
namespace EnterpriseAI.Application.Abstractions.Chunking;

public sealed record TextChunk(
    string Content,
    string? SectionTitle,
    int? PageNumber,
    int ChunkIndex,
    int EstimatedTokenCount);
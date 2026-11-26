namespace EnterpriseAI.Application.Abstractions.Chunking;

public sealed record ChunkingResult(
    IReadOnlyList<TextChunk> Chunks)
{
    public int Count => Chunks.Count;
    public int TotalCharacterCount => Chunks.Sum(chunk => chunk.Content.Length);
    public int EstimatedTotalTokenCount => Chunks.Sum(chunk => chunk.EstimatedTokenCount);
}
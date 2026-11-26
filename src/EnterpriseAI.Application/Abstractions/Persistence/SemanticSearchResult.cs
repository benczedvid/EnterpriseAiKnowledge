namespace EnterpriseAI.Application.Abstractions.Persistence;

public sealed record SemanticSearchResult(
    Guid DocumentId,
    Guid ChunkId,
    string SourceId,
    string DocumentTitle,
    string SourceUrl,
    string Content,
    string? SectionTitle,
    int? PageNumber,
    int ChunkIndex,
    double Distance)
{
    public double Similarity => 1 - Distance;
}
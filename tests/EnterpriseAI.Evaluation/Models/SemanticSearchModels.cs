namespace EnterpriseAI.Evaluation.Models;

public sealed record SemanticSearchApiRequest(
    string Query,
    int Limit);

public sealed record SemanticSearchApiResponse(
    string Query,
    int ResultCount,
    IReadOnlyList<SemanticSearchResult> Results);

public sealed record SemanticSearchResult(
    Guid DocumentId,
    Guid ChunkId,
    string? SourceId,
    string DocumentTitle,
    string SourceUrl,
    string Content,
    string? SectionTitle,
    int? PageNumber,
    int ChunkIndex,
    double Distance,
    double Similarity);
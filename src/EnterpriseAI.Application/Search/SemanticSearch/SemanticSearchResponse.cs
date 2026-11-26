namespace EnterpriseAI.Application.Search.SemanticSearch;

public sealed record SemanticSearchResponse(
    string Query,
    int ResultCount,
    IReadOnlyList<SemanticSearchItemResponse> Results);

public sealed record SemanticSearchItemResponse(
    Guid DocumentId,
    Guid ChunkId,
    string SourceId,
    string DocumentTitle,
    string SourceUrl,
    string Content,
    string? SectionTitle,
    int? PageNumber,
    int ChunkIndex,
    double Distance,
    double Similarity);
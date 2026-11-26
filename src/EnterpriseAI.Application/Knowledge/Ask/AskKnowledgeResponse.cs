namespace EnterpriseAI.Application.Knowledge.Ask;

public sealed record AskKnowledgeResponse(
    string Question,
    string Answer,
    bool AnsweredFromKnowledgeBase,
    IReadOnlyList<KnowledgeSourceResponse> Sources);

public sealed record KnowledgeSourceResponse(
    Guid DocumentId,
    Guid ChunkId,
    string SourceId,
    string DocumentTitle,
    string SourceUrl,
    string? SectionTitle,
    int? PageNumber,
    int ChunkIndex,
    double Similarity);
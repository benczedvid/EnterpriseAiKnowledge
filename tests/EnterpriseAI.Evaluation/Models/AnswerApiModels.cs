namespace EnterpriseAI.Evaluation.Models;

public sealed record AskKnowledgeApiRequest(
    string Question);

public sealed record AskKnowledgeApiResponse(
    string Question,
    string Answer,
    bool AnsweredFromKnowledgeBase,
    IReadOnlyList<KnowledgeSourceApiResponse> Sources);

public sealed record KnowledgeSourceApiResponse(
    Guid DocumentId,
    Guid ChunkId,
    string SourceId,
    string DocumentTitle,
    string SourceUrl,
    string? SectionTitle,
    int? PageNumber,
    int ChunkIndex,
    double Similarity);
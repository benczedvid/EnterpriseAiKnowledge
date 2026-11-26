namespace EnterpriseAI.Evaluation.Models;

public sealed record DocumentGoldenDataset(
    string DocumentKey,
    string SourceId,
    string FileName,
    string Format,
    string DatasetVersion,
    IReadOnlyList<RetrievalGoldenCase> Cases);

public sealed record RetrievalGoldenCase(
    string Id,
    string Question,
    string Category,
    bool ShouldAnswer,
    IReadOnlyList<string> ExpectedSourceIds,
    string? ExpectedDocumentTitleContains,
    string? ExpectedSectionTitleContains,
    IReadOnlyList<string> RequiredContentTerms,
    IReadOnlyList<string> RequiredAnswerFacts,
    IReadOnlyList<string> ForbiddenAnswerClaims,
    int ExpectedTopK,
    double? MinimumSimilarity,
    string? Notes,
    RetrievalBaseline? Baseline);

public sealed record RetrievalBaseline(
    int Rank,
    double Similarity,
    int ChunkIndex);
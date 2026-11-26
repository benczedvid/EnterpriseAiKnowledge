namespace EnterpriseAI.Evaluation.Models;

public sealed record RetrievalCaseResult(
    string CaseId,
    string Category,
    string Question,
    bool ShouldAnswer,
    bool DocumentFound,
    bool CorrectSectionFound,
    bool RequiredTermsFound,
    bool SimilarityPassed,
    bool Passed,
    int? FirstDocumentRank,
    int? FirstCorrectSectionRank,
    double? CorrectSectionSimilarity,
    string? ActualDocumentTitle,
    string? ActualSectionTitle,
    int? ActualChunkIndex,
    IReadOnlyList<string> MissingRequiredTerms,
    string? FailureReason);

public sealed record RetrievalEvaluationSummary(
    string DatasetVersion,
    DateTimeOffset EvaluatedAt,
    string ApiBaseUrl,
    int CaseCount,
    int AnswerableCaseCount,
    int NoAnswerCaseCount,
    int PassedCount,
    int FailedCount,
    double PassRate,
    double DocumentHitAt1,
    double DocumentRecallAt3,
    double DocumentRecallAt5,
    double SectionHitAt1,
    double SectionRecallAt3,
    double SectionRecallAt5,
    double MeanReciprocalRank,
    IReadOnlyList<RetrievalCaseResult> Results);
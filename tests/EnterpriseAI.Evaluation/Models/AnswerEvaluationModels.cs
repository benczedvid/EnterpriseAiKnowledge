namespace EnterpriseAI.Evaluation.Models;

public sealed record AnswerCaseResult(
    string CaseId,
    string Category,
    string Question,
    bool ShouldAnswer,
    bool AnsweredFromKnowledgeBase,
    bool ClassificationPassed,
    bool NonEmptyAnswerPassed,
    bool SourcePresencePassed,
    bool ExpectedSourcePassed,
    bool ForbiddenClaimsPassed,
    bool CitationPassed,
    bool Passed,
    int SourceCount,
    IReadOnlyList<string> ActualSourceIds,
    IReadOnlyList<string> MatchedForbiddenClaims,
    string? FailureReason,
    string Answer);

public sealed record AnswerEvaluationSummary(
    string DatasetVersion,
    DateTimeOffset EvaluatedAt,
    string ApiBaseUrl,
    int CaseCount,
    int AnswerableCaseCount,
    int NoAnswerCaseCount,
    int PassedCount,
    int FailedCount,
    double PassRate,
    double ClassificationAccuracy,
    double NonEmptyAnswerRate,
    double SourcePresenceRate,
    double ExpectedSourceRecall,
    double ForbiddenClaimPassRate,
    double CitationPassRate,
    IReadOnlyList<AnswerCaseResult> Results);

public sealed record AnswerEvaluationQualityGate(
    double MinimumPassRate,
    double MinimumClassificationAccuracy,
    double MinimumNonEmptyAnswerRate,
    double MinimumSourcePresenceRate,
    double MinimumExpectedSourceRecall,
    double MinimumForbiddenClaimPassRate,
    double MinimumCitationPassRate);

public sealed record AnswerEvaluationOutput(
    AnswerEvaluationSummary Summary,
    AnswerEvaluationQualityGate QualityGate,
    bool QualityGatePassed,
    IReadOnlyList<string> QualityGateFailures);
namespace EnterpriseAI.Evaluation.Models;

public sealed record EvaluationQualityGate(
    double MinimumPassRate,
    double MinimumDocumentHitAt1,
    double MinimumDocumentRecallAt3,
    double MinimumSectionHitAt1,
    double MinimumSectionRecallAt3,
    double MinimumMeanReciprocalRank);
using EnterpriseAI.Evaluation.Models;

namespace EnterpriseAI.Evaluation;

public sealed class EvaluationQualityGateValidator
{
    public IReadOnlyList<string> Validate(
        RetrievalEvaluationSummary summary,
        EvaluationQualityGate qualityGate)
    {
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(qualityGate);

        var failures = new List<string>();

        ValidateMetric(
            name: "Pass rate",
            actual: summary.PassRate,
            minimum: qualityGate.MinimumPassRate,
            failures);

        ValidateMetric(
            name: "Document Hit@1",
            actual: summary.DocumentHitAt1,
            minimum: qualityGate.MinimumDocumentHitAt1,
            failures);

        ValidateMetric(
            name: "Document Recall@3",
            actual: summary.DocumentRecallAt3,
            minimum: qualityGate.MinimumDocumentRecallAt3,
            failures);

        ValidateMetric(
            name: "Section Hit@1",
            actual: summary.SectionHitAt1,
            minimum: qualityGate.MinimumSectionHitAt1,
            failures);

        ValidateMetric(
            name: "Section Recall@3",
            actual: summary.SectionRecallAt3,
            minimum: qualityGate.MinimumSectionRecallAt3,
            failures);

        ValidateMetric(
            name: "MRR",
            actual: summary.MeanReciprocalRank,
            minimum:
                qualityGate.MinimumMeanReciprocalRank,
            failures);

        return failures;
    }

    private static void ValidateMetric(
        string name,
        double actual,
        double minimum,
        ICollection<string> failures)
    {
        if (actual >= minimum)
        {
            return;
        }

        failures.Add($"{name} is {actual:F3}, below the required minimum of {minimum:F3}.");
    }
}
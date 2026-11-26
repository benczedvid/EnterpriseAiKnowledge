using EnterpriseAI.Evaluation.Models;

namespace EnterpriseAI.Evaluation;

public sealed class AnswerQualityGateValidator
{
    public IReadOnlyList<string> Validate(AnswerEvaluationSummary summary, AnswerEvaluationQualityGate qualityGate)
    {
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(qualityGate);

        var failures = new List<string>();

        ValidateMetric(
            "Answer pass rate",
            summary.PassRate,
            qualityGate.MinimumPassRate,
            failures);

        ValidateMetric(
            "Classification accuracy",
            summary.ClassificationAccuracy,
            qualityGate.MinimumClassificationAccuracy,
            failures);

        ValidateMetric(
            "Non-empty answer rate",
            summary.NonEmptyAnswerRate,
            qualityGate.MinimumNonEmptyAnswerRate,
            failures);

        ValidateMetric(
            "Source presence rate",
            summary.SourcePresenceRate,
            qualityGate.MinimumSourcePresenceRate,
            failures);

        ValidateMetric(
            "Expected source recall",
            summary.ExpectedSourceRecall,
            qualityGate.MinimumExpectedSourceRecall,
            failures);

        ValidateMetric(
            "Forbidden claim pass rate",
            summary.ForbiddenClaimPassRate,
            qualityGate.MinimumForbiddenClaimPassRate,
            failures);

        ValidateMetric(
            "Citation pass rate",
            summary.CitationPassRate,
            qualityGate.MinimumCitationPassRate,
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
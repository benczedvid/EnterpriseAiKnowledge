using System.Text.RegularExpressions;
using EnterpriseAI.Evaluation.Models;

namespace EnterpriseAI.Evaluation;

public sealed partial class AnswerEvaluator
{
    private readonly KnowledgeAnswerClient _answerClient;

    public AnswerEvaluator(KnowledgeAnswerClient answerClient)
    {
        ArgumentNullException.ThrowIfNull(answerClient);

        _answerClient = answerClient;
    }

    public async Task<AnswerEvaluationSummary> EvaluateAsync(
        IReadOnlyList<DocumentGoldenDataset> datasets,
        string datasetVersion,
        string apiBaseUrl,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(datasets);

        var goldenCases = datasets
            .SelectMany(dataset => dataset.Cases)
            .ToArray();

        var results = new List<AnswerCaseResult>(goldenCases.Length);

        foreach (var goldenCase in goldenCases)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Console.Write($"[{results.Count + 1}/{goldenCases.Length}] {goldenCase.Id}: ");

            AnswerCaseResult result;

            try
            {
                var response = await _answerClient.AskAsync(goldenCase.Question, cancellationToken);

                result = EvaluateCase(goldenCase, response);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException and not OperationCanceledException)
            {
                result = CreateRequestFailure(goldenCase, exception);
            }

            results.Add(result);

            Console.ForegroundColor = result.Passed ? ConsoleColor.Green : ConsoleColor.Red;

            Console.WriteLine(result.Passed ? "PASS" : $"FAIL - {result.FailureReason}");

            Console.ResetColor();
        }

        return CreateSummary(datasetVersion, apiBaseUrl, results);
    }

    private static AnswerCaseResult EvaluateCase(RetrievalGoldenCase goldenCase, AskKnowledgeApiResponse response)
    {
        var answer = response.Answer?.Trim() ?? string.Empty;

        var actualSourceIds = response.Sources
            .Select(source => source.SourceId)
            .Where(sourceId => !string.IsNullOrWhiteSpace(sourceId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var classificationPassed = response.AnsweredFromKnowledgeBase == goldenCase.ShouldAnswer;

        var nonEmptyAnswerPassed = !string.IsNullOrWhiteSpace(answer);

        var sourcePresencePassed = goldenCase.ShouldAnswer
                ? response.Sources.Count > 0
                : response.Sources.Count == 0;

        var expectedSourcePassed =
            !goldenCase.ShouldAnswer
            || goldenCase.ExpectedSourceIds.Count == 0
            || goldenCase.ExpectedSourceIds.Any(
                expectedSourceId =>
                    actualSourceIds.Contains(
                        expectedSourceId,
                        StringComparer.Ordinal));

        var matchedForbiddenClaims =
            goldenCase.ForbiddenAnswerClaims
                .Where(claim =>
                    ContainsText(
                        answer,
                        claim))
                .ToArray();

        var forbiddenClaimsPassed = matchedForbiddenClaims.Length == 0;

        var citationPassed = !goldenCase.ShouldAnswer || HasCitation(answer);

        var failures = new List<string>();

        if (!classificationPassed)
        {
            failures.Add(
                goldenCase.ShouldAnswer
                    ? "The endpoint did not answer an answerable question from the knowledge base."
                    : "The endpoint answered a no-answer question from the knowledge base.");
        }

        if (!nonEmptyAnswerPassed)
        {
            failures.Add("The answer is empty.");
        }

        if (!sourcePresencePassed)
        {
            failures.Add(goldenCase.ShouldAnswer
                    ? "The answer contains no sources."
                    : "The no-answer response contains sources.");
        }

        if (!expectedSourcePassed)
        {
            failures.Add("The expected source was not included.");
        }

        if (!forbiddenClaimsPassed)
        {
            failures.Add("The answer contains a forbidden claim: "
                + string.Join(", ", matchedForbiddenClaims));
        }

        if (!citationPassed)
        {
            failures.Add("The answer contains no numeric citation.");
        }

        var passed =
            classificationPassed
            && nonEmptyAnswerPassed
            && sourcePresencePassed
            && expectedSourcePassed
            && forbiddenClaimsPassed
            && citationPassed;

        return new AnswerCaseResult(
            CaseId: goldenCase.Id,
            Category: goldenCase.Category,
            Question: goldenCase.Question,
            ShouldAnswer: goldenCase.ShouldAnswer,
            AnsweredFromKnowledgeBase: response.AnsweredFromKnowledgeBase,
            ClassificationPassed: classificationPassed,
            NonEmptyAnswerPassed: nonEmptyAnswerPassed,
            SourcePresencePassed: sourcePresencePassed,
            ExpectedSourcePassed: expectedSourcePassed,
            ForbiddenClaimsPassed: forbiddenClaimsPassed,
            CitationPassed: citationPassed,
            Passed: passed,
            SourceCount: response.Sources.Count,
            ActualSourceIds: actualSourceIds,
            MatchedForbiddenClaims: matchedForbiddenClaims,
            FailureReason:
                failures.Count == 0
                    ? null
                    : string.Join(
                        " ",
                        failures),
            Answer: answer);
    }

    private static AnswerCaseResult CreateRequestFailure(RetrievalGoldenCase goldenCase, Exception exception)
    {
        return new AnswerCaseResult(
            CaseId: goldenCase.Id,
            Category: goldenCase.Category,
            Question: goldenCase.Question,
            ShouldAnswer: goldenCase.ShouldAnswer,
            AnsweredFromKnowledgeBase: false,
            ClassificationPassed: false,
            NonEmptyAnswerPassed: false,
            SourcePresencePassed: false,
            ExpectedSourcePassed: false,
            ForbiddenClaimsPassed: false,
            CitationPassed: false,
            Passed: false,
            SourceCount: 0,
            ActualSourceIds: [],
            MatchedForbiddenClaims: [],
            FailureReason: $"Answer request failed: {exception.Message}",
            Answer: string.Empty);
    }

    private static AnswerEvaluationSummary CreateSummary(
        string datasetVersion,
        string apiBaseUrl,
        IReadOnlyList<AnswerCaseResult> results)
    {
        var answerableCount = results.Count(result => result.ShouldAnswer);

        var noAnswerCount = results.Count - answerableCount;

        var passedCount = results.Count(result => result.Passed);

        return new AnswerEvaluationSummary(
            DatasetVersion: datasetVersion,
            EvaluatedAt: DateTimeOffset.UtcNow,
            ApiBaseUrl: apiBaseUrl,
            CaseCount: results.Count,
            AnswerableCaseCount: answerableCount,
            NoAnswerCaseCount: noAnswerCount,
            PassedCount: passedCount,
            FailedCount: results.Count - passedCount,
            PassRate: Divide(passedCount, results.Count),
            ClassificationAccuracy: Divide(results.Count(result => result.ClassificationPassed), results.Count),
            NonEmptyAnswerRate: Divide(results.Count(result => result.NonEmptyAnswerPassed), results.Count),
            SourcePresenceRate: Divide(results.Count(result => result.SourcePresencePassed), results.Count),
            ExpectedSourceRecall:
                Divide(
                    results.Count(result =>
                        !result.ShouldAnswer
                        || result.ExpectedSourcePassed),
                    results.Count),
            ForbiddenClaimPassRate: Divide(results.Count(result => result.ForbiddenClaimsPassed), results.Count),
            CitationPassRate:
                Divide(
                    results.Count(result =>
                        !result.ShouldAnswer
                        || result.CitationPassed),
                    results.Count),
            Results: results);
    }

    private static bool ContainsText(string text, string expected)
    {
        return text.Contains(expected, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasCitation(string answer)
    {
        return CitationRegex().IsMatch(answer);
    }

    private static double Divide(int numerator, int denominator)
    {
        return denominator == 0 ? 0 : (double)numerator / denominator;
    }

    [GeneratedRegex(@"\[\s*\d+(?:\s*,\s*\d+)*\s*\]", RegexOptions.CultureInvariant)]
    private static partial Regex CitationRegex();
}
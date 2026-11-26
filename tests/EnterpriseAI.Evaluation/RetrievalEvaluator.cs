using EnterpriseAI.Evaluation.Models;

namespace EnterpriseAI.Evaluation;

public sealed class RetrievalEvaluator
{
    private const int EvaluationSearchLimit = 10;
    private readonly SemanticSearchClient _searchClient;

    public RetrievalEvaluator(SemanticSearchClient searchClient)
    {
        ArgumentNullException.ThrowIfNull(searchClient);

        _searchClient = searchClient;
    }

    public async Task<RetrievalEvaluationSummary>EvaluateAsync(
            IReadOnlyList<DocumentGoldenDataset> datasets,
            string datasetVersion,
            string apiBaseUrl,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(datasets);

        var goldenCases = datasets
            .SelectMany(dataset => dataset.Cases)
            .ToArray();

        var results = new List<RetrievalCaseResult>(goldenCases.Length);

        foreach (var goldenCase in goldenCases)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Console.Write(
                $"[{results.Count + 1}/{goldenCases.Length}] " +
                $"{goldenCase.Id}: ");

            RetrievalCaseResult result;

            try
            {
                var searchResponse =
                    await _searchClient.SearchAsync(
                        goldenCase.Question,
                        Math.Max(
                            EvaluationSearchLimit,
                            goldenCase.ExpectedTopK),
                        cancellationToken);

                result = EvaluateCase(goldenCase, searchResponse.Results);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException and not OperationCanceledException)
            {
                result = CreateRequestFailure(goldenCase, exception);
            }

            results.Add(result);

            Console.ForegroundColor = result.Passed
                ? ConsoleColor.Green
                : ConsoleColor.Red;

            Console.WriteLine(
                result.Passed
                    ? "PASS"
                    : $"FAIL - {result.FailureReason}");

            Console.ResetColor();
        }

        return CreateSummary(
            datasetVersion,
            apiBaseUrl,
            results);
    }

    private static RetrievalCaseResult EvaluateCase(RetrievalGoldenCase goldenCase, IReadOnlyList<SemanticSearchResult> searchResults)
    {
        var rankedResults = searchResults
            .Select(
                (result, index) =>
                    new RankedResult(
                        Rank: index + 1,
                        Result: result))
            .ToArray();

        var documentMatches = rankedResults
            .Where(candidate =>
                IsExpectedDocument(
                    goldenCase,
                    candidate.Result))
            .ToArray();

        var firstDocumentMatch = documentMatches.FirstOrDefault();

        var correctSectionMatches = documentMatches
            .Where(candidate =>
                IsExpectedSection(
                    goldenCase,
                    candidate.Result)
                && ContainsAllRequiredTerms(
                    goldenCase,
                    candidate.Result))
            .ToArray();


        var firstCorrectSection = correctSectionMatches.FirstOrDefault();

        var relevantResult = firstCorrectSection?.Result ?? firstDocumentMatch?.Result;

        var searchableResultText = relevantResult is null ? string.Empty : CreateSearchableResultText(relevantResult);

        var missingTerms = goldenCase.RequiredContentTerms
            .Where(term => !ContainsText(searchableResultText, term))
            .ToArray();

        var documentFound = firstDocumentMatch is not null;
        var correctSectionFound = firstCorrectSection is not null;
        var termsFound = missingTerms.Length == 0;

        var similarityPassed =
            goldenCase.MinimumSimilarity is null
            || (
                firstCorrectSection is not null
                && firstCorrectSection.Result.Similarity
                    >= goldenCase.MinimumSimilarity.Value
            );

        bool passed;
        string? failureReason;

        if (!goldenCase.ShouldAnswer)
        {
            /*
             * A retrieval önmagában mindig visszaadhat
             * szemantikailag hasonló találatokat.
             *
             * Egy no-answer eset akkor bukik retrieval szinten,
             * ha az első találat eléri a golden esetben megadott
             * minimum similarity értéket. Ha nincs threshold,
             * csak az eredményt rögzítjük, nem tekintjük hibának.
             */
            passed =
                goldenCase.MinimumSimilarity is null
                || searchResults.Count == 0
                || searchResults[0].Similarity
                    < goldenCase.MinimumSimilarity.Value;

            failureReason = passed
                ? null
                : "No-answer case returned a result above the allowed similarity threshold.";
        }
        else if (!documentFound)
        {
            passed = false;
            failureReason = "Expected document was not found in the evaluated result set.";
        }
        else if (!correctSectionFound)
        {
            passed = false;
            failureReason = "Expected section was not found.";
        }
        else if (firstCorrectSection!.Rank > goldenCase.ExpectedTopK)
        {
            passed = false;
            failureReason =
                $"Correct section rank " +
                $"{firstCorrectSection.Rank} exceeds " +
                $"expected Top-{goldenCase.ExpectedTopK}.";
        }
        else if (!termsFound)
        {
            passed = false;
            failureReason = "Required terms are missing from the retrieved section.";
        }
        else if (!similarityPassed)
        {
            passed = false;
            failureReason = "Correct section similarity is below the configured minimum.";
        }
        else
        {
            passed = true;
            failureReason = null;
        }

        return new RetrievalCaseResult(
            CaseId: goldenCase.Id,
            Category: goldenCase.Category,
            Question: goldenCase.Question,
            ShouldAnswer: goldenCase.ShouldAnswer,
            DocumentFound: documentFound,
            CorrectSectionFound: correctSectionFound,
            RequiredTermsFound: termsFound,
            SimilarityPassed: similarityPassed,
            Passed: passed,
            FirstDocumentRank: firstDocumentMatch?.Rank,
            FirstCorrectSectionRank: firstCorrectSection?.Rank,
            CorrectSectionSimilarity: firstCorrectSection?.Result.Similarity,
            ActualDocumentTitle: firstCorrectSection?.Result.DocumentTitle ?? firstDocumentMatch?.Result.DocumentTitle,
            ActualSectionTitle: firstCorrectSection?.Result.SectionTitle,
            ActualChunkIndex: firstCorrectSection?.Result.ChunkIndex,
            MissingRequiredTerms: missingTerms,
            FailureReason: failureReason);
    }

    private static bool IsExpectedDocument(
        RetrievalGoldenCase goldenCase,
        SemanticSearchResult result)
    {
        if (goldenCase.ExpectedSourceIds.Count > 0 && !string.IsNullOrWhiteSpace(result.SourceId))
        {
            return goldenCase.ExpectedSourceIds.Any(
                sourceId =>
                    string.Equals(
                        sourceId,
                        result.SourceId,
                        StringComparison.Ordinal));
        }

        if (string.IsNullOrWhiteSpace(goldenCase.ExpectedDocumentTitleContains))
        {
            return false;
        }

        return ContainsText(
            result.DocumentTitle,
            goldenCase.ExpectedDocumentTitleContains);
    }

    private static bool IsExpectedSection(
        RetrievalGoldenCase goldenCase,
        SemanticSearchResult result)
    {
        if (string.IsNullOrWhiteSpace(goldenCase.ExpectedSectionTitleContains))
        {
            return true;
        }

        return ContainsText(result.SectionTitle, goldenCase.ExpectedSectionTitleContains);
    }

    private static bool ContainsText(string? text, string expected)
    {
        return !string.IsNullOrWhiteSpace(text)
            && text.Contains(
                expected,
                StringComparison
                    .OrdinalIgnoreCase);
    }

    private static RetrievalCaseResult CreateRequestFailure(RetrievalGoldenCase goldenCase, Exception exception)
    {
        return new RetrievalCaseResult(
            CaseId: goldenCase.Id,
            Category: goldenCase.Category,
            Question: goldenCase.Question,
            ShouldAnswer: goldenCase.ShouldAnswer,
            DocumentFound: false,
            CorrectSectionFound: false,
            RequiredTermsFound: false,
            SimilarityPassed: false,
            Passed: false,
            FirstDocumentRank: null,
            FirstCorrectSectionRank: null,
            CorrectSectionSimilarity: null,
            ActualDocumentTitle: null,
            ActualSectionTitle: null,
            ActualChunkIndex: null,
            MissingRequiredTerms: goldenCase.RequiredContentTerms,
            FailureReason: $"Evaluation request failed: {exception.Message}");
    }

    private static RetrievalEvaluationSummary CreateSummary(
        string datasetVersion,
        string apiBaseUrl,
        IReadOnlyList<RetrievalCaseResult> results)
    {
        var answerable = results
            .Where(result => result.ShouldAnswer)
            .ToArray();

        var noAnswerCount = results.Count - answerable.Length;

        var documentHitAt1 = Divide(
            answerable.Count(result => result.FirstDocumentRank == 1),
            answerable.Length);

        var documentRecallAt3 = Divide(
            answerable.Count(result => result.FirstDocumentRank is <= 3),
            answerable.Length);

        var documentRecallAt5 = Divide(
            answerable.Count(result => result.FirstDocumentRank is <= 5),
            answerable.Length);

        var sectionHitAt1 = Divide(
            answerable.Count(result => result.FirstCorrectSectionRank == 1),
            answerable.Length);

        var sectionRecallAt3 = Divide(
            answerable.Count(result => result.FirstCorrectSectionRank is <= 3),
            answerable.Length);

        var sectionRecallAt5 = Divide(
            answerable.Count(result => result.FirstCorrectSectionRank is <= 5),
            answerable.Length);

        var reciprocalRankSum = answerable.Sum(
            result =>
                result.FirstCorrectSectionRank.HasValue
                    ? 1.0
                      / result
                          .FirstCorrectSectionRank.Value
                    : 0.0);

        var meanReciprocalRank = answerable.Length == 0
                ? 0
                : reciprocalRankSum / answerable.Length;

        var passedCount = results.Count(result => result.Passed);

        return new RetrievalEvaluationSummary(
            DatasetVersion: datasetVersion,
            EvaluatedAt: DateTimeOffset.UtcNow,
            ApiBaseUrl: apiBaseUrl,
            CaseCount: results.Count,
            AnswerableCaseCount: answerable.Length,
            NoAnswerCaseCount: noAnswerCount,
            PassedCount: passedCount,
            FailedCount: results.Count - passedCount,
            PassRate: Divide(passedCount, results.Count),
            DocumentHitAt1: documentHitAt1,
            DocumentRecallAt3: documentRecallAt3,
            DocumentRecallAt5: documentRecallAt5,
            SectionHitAt1: sectionHitAt1,
            SectionRecallAt3: sectionRecallAt3,
            SectionRecallAt5: sectionRecallAt5,
            MeanReciprocalRank: meanReciprocalRank,
            Results: results);
    }

    private static double Divide(int numerator, int denominator)
    {
        return denominator == 0 ? 0 : (double)numerator / denominator;
    }
    private static bool ContainsAllRequiredTerms(RetrievalGoldenCase goldenCase, SemanticSearchResult result)
    {
        if (goldenCase.RequiredContentTerms.Count == 0)
        {
            return true;
        }

        var searchableText = CreateSearchableResultText(result);

        return goldenCase.RequiredContentTerms.All(term => ContainsText(searchableText, term));
    }

    private static string CreateSearchableResultText(SemanticSearchResult result)
    {
        return string.Join(
            Environment.NewLine,
            result.DocumentTitle,
            result.SectionTitle ?? string.Empty,
            result.Content);
    }

    private sealed record RankedResult(int Rank, SemanticSearchResult Result);
}
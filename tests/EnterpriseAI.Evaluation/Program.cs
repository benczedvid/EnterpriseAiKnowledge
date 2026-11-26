using System.Text.Json;
using EnterpriseAI.Evaluation;
using EnterpriseAI.Evaluation.Models;

const string DefaultApiBaseUrl = "http://localhost:5241/";
const string DefaultEvaluationMode = "retrieval";

var apiBaseUrl = args.Length > 0 ? args[0] : DefaultApiBaseUrl;
var evaluationMode = args.Length > 1 ? args[1].Trim().ToLowerInvariant() : DefaultEvaluationMode;

if (!Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var apiUri))
{
    Console.Error.WriteLine($"Invalid API base URL: '{apiBaseUrl}'.");

    return 2;
}

if (evaluationMode is not "retrieval" and not "answer")
{
    Console.Error.WriteLine(
        $"Unknown evaluation mode: " +
        $"'{evaluationMode}'. " +
        "Supported values: retrieval, answer.");

    return 2;
}

var goldenDatasetDirectory = Path.Combine(
    AppContext.BaseDirectory,
    "GoldenDatasets",
    "cases");

Console.WriteLine("Enterprise AI Evaluation");
Console.WriteLine($"Mode: {evaluationMode}");
Console.WriteLine($"API: {apiUri}");
Console.WriteLine($"Dataset: {goldenDatasetDirectory}");

Console.WriteLine();

using var cancellationSource = new CancellationTokenSource();

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellationSource.Cancel();
};

try
{
    var loader = new GoldenDatasetLoader();

    var datasets = await loader.LoadAsync(goldenDatasetDirectory, cancellationSource.Token);

    var datasetVersion = string.Join(
        "+",
        datasets
            .Select(dataset => dataset.DatasetVersion)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(version => version));

    if (evaluationMode == "retrieval")
    {
        return await RunRetrievalEvaluationAsync(
            datasets,
            datasetVersion,
            apiUri,
            cancellationSource.Token);
    }

    return await RunAnswerEvaluationAsync(
        datasets,
        datasetVersion,
        apiUri,
        cancellationSource.Token);
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Evaluation was cancelled.");

    return 130;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Evaluation failed: {exception.Message}");

    return 2;
}

static async Task<int> RunRetrievalEvaluationAsync(
    IReadOnlyList<DocumentGoldenDataset> datasets,
    string datasetVersion,
    Uri apiUri,
    CancellationToken cancellationToken)
{
    using var httpClient = CreateHttpClient(apiUri);

    var searchClient = new SemanticSearchClient(httpClient);
    var evaluator = new RetrievalEvaluator(searchClient);

    Console.WriteLine("Retrieval evaluation");
    Console.WriteLine();

    var summary = await evaluator.EvaluateAsync(
        datasets,
        datasetVersion,
        apiUri.ToString(),
        cancellationToken);

    var qualityGate = new EvaluationQualityGate(
            MinimumPassRate: 1.00,
            MinimumDocumentHitAt1: 0.90,
            MinimumDocumentRecallAt3: 1.00,
            MinimumSectionHitAt1: 0.55,
            MinimumSectionRecallAt3: 1.00,
            MinimumMeanReciprocalRank: 0.75);

    var validator = new EvaluationQualityGateValidator();

    var qualityGateFailures = validator.Validate(summary, qualityGate);

    PrintRetrievalSummary(summary);
    PrintQualityGate(qualityGateFailures);

    var output = new RetrievalEvaluationOutput(
        Summary: summary,
        QualityGate: qualityGate,
        QualityGatePassed: qualityGateFailures.Count == 0,
        QualityGateFailures: qualityGateFailures);

    var outputPath = await WriteOutputAsync(
            filePrefix: "retrieval-evaluation",
            output,
            cancellationToken);

    Console.WriteLine();
    Console.WriteLine($"Result saved to: {outputPath}");

    return summary.FailedCount == 0 && qualityGateFailures.Count == 0 ? 0 : 1;
}

static async Task<int> RunAnswerEvaluationAsync(
    IReadOnlyList<DocumentGoldenDataset> datasets,
    string datasetVersion,
    Uri apiUri,
    CancellationToken cancellationToken)
{
    /*
     * Első körben csak a reprezentatív tíz golden esetet
     * futtatjuk, mert minden eset külön helyi LLM-hívást
     * eredményez.
     */
    var selectedCaseIds =
        new HashSet<string>(
            [
                "digital-receipt-001",
                "digital-receipt-005",
                "digital-receipt-007",
                "gate-pass-001",
                "gate-pass-003",
                "gate-pass-004",
                "stock-clearance-close-001",
                "stock-clearance-close-003",
                "unused-cash-advance-001",
                "unused-cash-advance-004",
                "digital-receipt-011",
                
                "digital-receipt-012",
                "gate-pass-005",
                "gate-pass-006",
                "stock-clearance-close-007",
                "unused-cash-advance-010",
                "unused-cash-advance-011"
            ],
            StringComparer.OrdinalIgnoreCase);

    var selectedDatasets = datasets
        .Select(dataset =>
            dataset with
            {
                Cases = dataset.Cases
                    .Where(goldenCase => selectedCaseIds.Contains(goldenCase.Id))
                    .ToArray()
            })
        .Where(dataset => dataset.Cases.Count > 0)
        .ToArray();

    var selectedCaseCount = selectedDatasets.Sum(dataset => dataset.Cases.Count);

    if (selectedCaseCount == 0)
    {
        throw new InvalidOperationException("No answer evaluation cases were selected.");
    }

    using var httpClient = CreateHttpClient(apiUri);

    var answerClient = new KnowledgeAnswerClient(httpClient);
    var evaluator = new AnswerEvaluator(answerClient);

    Console.WriteLine("Answer quality evaluation");
    Console.WriteLine($"Selected cases: {selectedCaseCount}");
    Console.WriteLine();

    var summary = await evaluator.EvaluateAsync(
        selectedDatasets,
        datasetVersion,
        apiUri.ToString(),
        cancellationToken);

    var qualityGate = new AnswerEvaluationQualityGate(
            MinimumPassRate: 1.00,
            MinimumClassificationAccuracy: 1.00,
            MinimumNonEmptyAnswerRate: 1.00,
            MinimumSourcePresenceRate: 1.00,
            MinimumExpectedSourceRecall: 1.00,
            MinimumForbiddenClaimPassRate: 1.00,
            MinimumCitationPassRate: 1.00);

    var validator = new AnswerQualityGateValidator();

    var qualityGateFailures = validator.Validate(summary, qualityGate);

    PrintAnswerSummary(summary);

    PrintQualityGate(qualityGateFailures);

    var output = new AnswerEvaluationOutput(
        Summary: summary,
        QualityGate: qualityGate,
        QualityGatePassed: qualityGateFailures.Count == 0,
        QualityGateFailures: qualityGateFailures);

    var outputPath = await WriteOutputAsync(
            filePrefix: "answer-evaluation",
            output,
            cancellationToken);

    Console.WriteLine();
    Console.WriteLine($"Result saved to: {outputPath}");

    return summary.FailedCount == 0 && qualityGateFailures.Count == 0 ? 0 : 1;
}

static HttpClient CreateHttpClient(Uri apiUri)
{
    return new HttpClient
    {
        BaseAddress = apiUri,
        /*
         * A helyi Ollama-válaszadás több perces is lehet
         * hosszabb promptoknál.
         */
        Timeout = TimeSpan.FromMinutes(10)
    };
}

static async Task<string> WriteOutputAsync<TOutput>(
    string filePrefix,
    TOutput output,
    CancellationToken cancellationToken)
{
    var outputDirectory = Path.Combine(
        AppContext.BaseDirectory,
        "EvaluationResults");

    Directory.CreateDirectory(outputDirectory);

    var outputPath = Path.Combine(
        outputDirectory,
        $"{filePrefix}-" +
        $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.json");

    var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

    await File.WriteAllTextAsync(
        outputPath,
        JsonSerializer.Serialize(output, jsonOptions),
        cancellationToken);

    return outputPath;
}

static void PrintRetrievalSummary(
    RetrievalEvaluationSummary summary)
{
    Console.WriteLine();
    Console.WriteLine("Retrieval summary");
    Console.WriteLine("-----------------");

    Console.WriteLine(
        $"Cases:              {summary.CaseCount}");

    Console.WriteLine(
        $"Passed:             {summary.PassedCount}");

    Console.WriteLine(
        $"Failed:             {summary.FailedCount}");

    Console.WriteLine(
        $"Pass rate:          {summary.PassRate:P1}");

    Console.WriteLine(
        $"Document Hit@1:     " +
        $"{summary.DocumentHitAt1:P1}");

    Console.WriteLine(
        $"Document Recall@3:  " +
        $"{summary.DocumentRecallAt3:P1}");

    Console.WriteLine(
        $"Document Recall@5:  " +
        $"{summary.DocumentRecallAt5:P1}");

    Console.WriteLine(
        $"Section Hit@1:      " +
        $"{summary.SectionHitAt1:P1}");

    Console.WriteLine(
        $"Section Recall@3:   " +
        $"{summary.SectionRecallAt3:P1}");

    Console.WriteLine(
        $"Section Recall@5:   " +
        $"{summary.SectionRecallAt5:P1}");

    Console.WriteLine(
        $"MRR:                " +
        $"{summary.MeanReciprocalRank:F3}");

    var failedCases = summary.Results
        .Where(result => !result.Passed)
        .ToArray();

    if (failedCases.Length == 0)
    {
        return;
    }

    Console.WriteLine();
    Console.WriteLine("Failed retrieval cases");
    Console.WriteLine("----------------------");

    foreach (var failedCase in failedCases)
    {
        Console.WriteLine(
            $"- {failedCase.CaseId}: " +
            $"{failedCase.FailureReason}");
    }
}

static void PrintAnswerSummary(
    AnswerEvaluationSummary summary)
{
    Console.WriteLine();
    Console.WriteLine("Answer summary");
    Console.WriteLine("--------------");

    Console.WriteLine(
        $"Cases:                   " +
        $"{summary.CaseCount}");

    Console.WriteLine(
        $"Passed:                  " +
        $"{summary.PassedCount}");

    Console.WriteLine(
        $"Failed:                  " +
        $"{summary.FailedCount}");

    Console.WriteLine(
        $"Pass rate:               " +
        $"{summary.PassRate:P1}");

    Console.WriteLine(
        $"Classification accuracy: " +
        $"{summary.ClassificationAccuracy:P1}");

    Console.WriteLine(
        $"Non-empty answer rate:   " +
        $"{summary.NonEmptyAnswerRate:P1}");

    Console.WriteLine(
        $"Source presence rate:    " +
        $"{summary.SourcePresenceRate:P1}");

    Console.WriteLine(
        $"Expected source recall:  " +
        $"{summary.ExpectedSourceRecall:P1}");

    Console.WriteLine(
        $"Forbidden claim pass:    " +
        $"{summary.ForbiddenClaimPassRate:P1}");

    Console.WriteLine(
        $"Citation pass rate:      " +
        $"{summary.CitationPassRate:P1}");

    var failedCases = summary.Results
        .Where(result => !result.Passed)
        .ToArray();

    if (failedCases.Length == 0)
    {
        return;
    }

    Console.WriteLine();
    Console.WriteLine("Failed answer cases");
    Console.WriteLine("-------------------");

    foreach (var failedCase in failedCases)
    {
        Console.WriteLine(
            $"- {failedCase.CaseId}: " +
            $"{failedCase.FailureReason}");
    }
}

static void PrintQualityGate(
    IReadOnlyList<string> failures)
{
    Console.WriteLine();

    if (failures.Count == 0)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Quality gate: PASS");
        Console.ResetColor();

        return;
    }

    Console.ForegroundColor = ConsoleColor.Red;

    Console.WriteLine("Quality gate: FAIL");

    foreach (var failure in failures)
    {
        Console.WriteLine($"- {failure}");
    }

    Console.ResetColor();
}

internal sealed record RetrievalEvaluationOutput(
    RetrievalEvaluationSummary Summary,
    EvaluationQualityGate QualityGate,
    bool QualityGatePassed,
    IReadOnlyList<string> QualityGateFailures);
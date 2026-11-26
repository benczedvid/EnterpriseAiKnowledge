using System.Text.Json;
using EnterpriseAI.Evaluation.Models;

namespace EnterpriseAI.Evaluation;

public sealed class GoldenDatasetLoader
{
    private static readonly JsonSerializerOptions JsonOptions =
        new()
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

    public async Task<IReadOnlyList<DocumentGoldenDataset>>LoadAsync(string directoryPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new ArgumentException("Golden dataset directory must not be empty.",
                nameof(directoryPath));
        }

        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException($"Golden dataset directory was not found: '{directoryPath}'.");
        }

        var files = Directory
            .EnumerateFiles(
                directoryPath,
                "*.json",
                SearchOption.TopDirectoryOnly)
            .OrderBy(
                file => file,
                StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (files.Length == 0)
        {
            throw new InvalidOperationException($"No golden dataset JSON files were found in '{directoryPath}'.");
        }

        var datasets = new List<DocumentGoldenDataset>();

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var stream = File.OpenRead(file);

            DocumentGoldenDataset? dataset;

            try
            {
                dataset = await JsonSerializer
                    .DeserializeAsync<DocumentGoldenDataset>(
                        stream,
                        JsonOptions,
                        cancellationToken);
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException(
                    $"Golden dataset file '{file}' contains invalid JSON.",
                    exception);
            }

            if (dataset is null)
            {
                throw new InvalidOperationException($"Golden dataset file '{file}' is empty.");
            }

            ValidateDataset(dataset, file);

            datasets.Add(dataset);
        }

        ValidateUniqueCaseIds(datasets);

        return datasets;
    }

    private static void ValidateDataset(DocumentGoldenDataset dataset, string file)
    {
        if (string.IsNullOrWhiteSpace(dataset.DocumentKey))
        {
            throw new InvalidOperationException($"Dataset '{file}' has no documentKey.");
        }

        if (dataset.Cases.Count == 0)
        {
            throw new InvalidOperationException($"Dataset '{file}' contains no cases.");
        }

        foreach (var goldenCase in dataset.Cases)
        {
            if (string.IsNullOrWhiteSpace(goldenCase.Id))
            {
                throw new InvalidOperationException($"Dataset '{file}' contains a case without an ID.");
            }

            if (string.IsNullOrWhiteSpace(goldenCase.Question))
            {
                throw new InvalidOperationException($"Case '{goldenCase.Id}' contains no question.");
            }

            if (goldenCase.ExpectedTopK < 1)
            {
                throw new InvalidOperationException($"Case '{goldenCase.Id}' has an invalid expectedTopK value.");
            }

            if (goldenCase.ShouldAnswer
                && goldenCase.ExpectedSourceIds.Count == 0
                && string.IsNullOrWhiteSpace(
                    goldenCase
                        .ExpectedDocumentTitleContains))
            {
                throw new InvalidOperationException(
                    $"Answerable case '{goldenCase.Id}' has " +
                    "neither expectedSourceIds nor an " +
                    "expected document title.");
            }
        }
    }

    private static void ValidateUniqueCaseIds(IReadOnlyList<DocumentGoldenDataset> datasets)
    {
        var duplicateIds = datasets
            .SelectMany(dataset => dataset.Cases)
            .GroupBy(goldenCase => goldenCase.Id, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        if (duplicateIds.Length == 0)
        {
            return;
        }

        throw new InvalidOperationException("Duplicate golden case IDs were found: " + string.Join(", ", duplicateIds));
    }
}
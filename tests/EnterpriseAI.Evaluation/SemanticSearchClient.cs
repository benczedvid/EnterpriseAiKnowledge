using System.Net.Http.Json;
using System.Text.Json;
using EnterpriseAI.Evaluation.Models;

namespace EnterpriseAI.Evaluation;

public sealed class SemanticSearchClient
{
    private static readonly JsonSerializerOptions JsonOptions =
        new()
        {
            PropertyNameCaseInsensitive = true
        };

    private readonly HttpClient _httpClient;

    public SemanticSearchClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        _httpClient = httpClient;
    }

    public async Task<SemanticSearchApiResponse> SearchAsync(
        string question,
        int limit,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            throw new ArgumentException("Question must not be empty.", nameof(question));
        }

        if (limit < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        using var response = await _httpClient.PostAsJsonAsync(
                "api/search/semantic",
                new SemanticSearchApiRequest(
                    Query: question.Trim(),
                    Limit: limit),
                JsonOptions,
                cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);

            throw new HttpRequestException(
                $"Semantic search request failed with " +
                $"HTTP {(int)response.StatusCode} " +
                $"({response.StatusCode}). Response: " +
                $"{errorContent}",
                inner: null,
                statusCode: response.StatusCode);
        }

        var result = await response.Content
            .ReadFromJsonAsync<SemanticSearchApiResponse>(JsonOptions, cancellationToken);

        return result
            ?? throw new InvalidOperationException("Semantic search returned an empty response.");
    }
}
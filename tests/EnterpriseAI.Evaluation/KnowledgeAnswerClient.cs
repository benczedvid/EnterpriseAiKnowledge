using System.Net.Http.Json;
using System.Text.Json;
using EnterpriseAI.Evaluation.Models;

namespace EnterpriseAI.Evaluation;

public sealed class KnowledgeAnswerClient
{
    private static readonly JsonSerializerOptions JsonOptions =
        new()
        {
            PropertyNameCaseInsensitive = true
        };

    private readonly HttpClient _httpClient;

    public KnowledgeAnswerClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        _httpClient = httpClient;
    }

    public async Task<AskKnowledgeApiResponse> AskAsync(string question, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            throw new ArgumentException("Question must not be empty.", nameof(question));
        }

        using var response = await _httpClient.PostAsJsonAsync("api/knowledge/ask",
                new AskKnowledgeApiRequest(Question: question.Trim()),
                JsonOptions,
                cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);

            throw new HttpRequestException(
                $"Knowledge request failed with " +
                $"HTTP {(int)response.StatusCode} " +
                $"({response.StatusCode}). Response: " +
                $"{errorContent}",
                inner: null,
                statusCode: response.StatusCode);
        }

        var result = await response.Content.ReadFromJsonAsync<AskKnowledgeApiResponse>(
                JsonOptions,
                cancellationToken);

        return result ?? throw new InvalidOperationException("Knowledge endpoint returned an empty response.");
    }
}
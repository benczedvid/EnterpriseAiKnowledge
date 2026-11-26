using System.Net.Http.Json;
using EnterpriseAI.Application.Abstractions.AI;
using Microsoft.Extensions.Options;

namespace EnterpriseAI.Infrastructure.AI.Ollama;

public sealed class OllamaEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly EmbeddingOptions _options;

    public OllamaEmbeddingService(HttpClient httpClient, IOptions<EmbeddingOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<EmbeddingResponse> CreateEmbeddingsAsync(EmbeddingRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Inputs.Count == 0)
        {
            throw new ArgumentException("At least one input is required.", nameof(request));
        }

        if (request.Inputs.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Embedding inputs must not contain empty values.", nameof(request));
        }

        var embeddings = new List<IReadOnlyList<float>>(request.Inputs.Count);

        foreach (var batch in request.Inputs.Chunk(_options.BatchSize))
        {
            var ollamaRequest = new OllamaEmbedRequest
            {
                Model = _options.Model,
                Input = batch,
                Truncate = _options.Truncate
            };

            using var response = await _httpClient.PostAsJsonAsync(
                "api/embed",
                ollamaRequest,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);

                throw new HttpRequestException(
                    $"Ollama embedding request failed with status " +
                    $"{(int)response.StatusCode} " +
                    $"({response.StatusCode}). " +
                    $"Response: {errorContent}",
                    inner: null,
                    statusCode: response.StatusCode);
            }

            var ollamaResponse = await response.Content.ReadFromJsonAsync<OllamaEmbedResponse>(cancellationToken: cancellationToken);

            if (ollamaResponse?.Embeddings is null)
            {
                throw new InvalidOperationException("Ollama returned no embeddings.");
            }

            if (ollamaResponse.Embeddings.Count != batch.Length)
            {
                throw new InvalidOperationException(
                    $"Ollama returned {ollamaResponse.Embeddings.Count} " +
                    $"embeddings for {batch.Length} inputs.");
            }

            foreach (var embedding in ollamaResponse.Embeddings)
            {
                if (embedding.Count != _options.Dimensions)
                {
                    throw new InvalidOperationException(
                        $"Ollama returned an embedding with " +
                        $"{embedding.Count} dimensions. " +
                        $"Expected {_options.Dimensions}.");
                }

                embeddings.Add(embedding);
            }
        }

        return new EmbeddingResponse(
            Embeddings: embeddings,
            Dimensions: _options.Dimensions);
    }
}
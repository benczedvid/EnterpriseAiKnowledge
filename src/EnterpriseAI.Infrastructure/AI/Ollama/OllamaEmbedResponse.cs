using System.Text.Json.Serialization;

namespace EnterpriseAI.Infrastructure.AI.Ollama;

internal sealed class OllamaEmbedResponse
{
    [JsonPropertyName("embeddings")]
    public IReadOnlyList<IReadOnlyList<float>>? Embeddings { get; init; }
}
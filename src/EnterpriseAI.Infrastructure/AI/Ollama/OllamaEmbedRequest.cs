using System.Text.Json.Serialization;

namespace EnterpriseAI.Infrastructure.AI.Ollama;

internal sealed class OllamaEmbedRequest
{
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("input")]
    public required IReadOnlyList<string> Input { get; init; }

    [JsonPropertyName("truncate")]
    public bool Truncate { get; init; }
}
using System.Text.Json.Serialization;

namespace EnterpriseAI.Infrastructure.AI.Ollama;

internal sealed class OllamaChatResponse
{
    [JsonPropertyName("message")]
    public OllamaChatMessage? Message { get; init; }

    [JsonPropertyName("done")]
    public bool Done { get; init; }
}
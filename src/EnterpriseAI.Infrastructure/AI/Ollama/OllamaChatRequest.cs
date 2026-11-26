using System.Text.Json;
using System.Text.Json.Serialization;

namespace EnterpriseAI.Infrastructure.AI.Ollama;

internal sealed class OllamaChatRequest
{
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("messages")]
    public required OllamaChatMessage[] Messages { get; init; }

    [JsonPropertyName("think")]
    public bool Think { get; init; }

    [JsonPropertyName("stream")]
    public bool Stream { get; init; }

    [JsonPropertyName("options")]
    public required OllamaChatOptions Options { get; init; }
    
    /*
     * Strukturált kimenet: JSON Schema, amelyhez az Ollama
     * a generálást korlátozza. Null esetén nem kerül a kérésbe.
     */
    [JsonPropertyName("format")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Format { get; init; }

    [JsonPropertyName("keep_alive")]
    public string KeepAlive { get; init; } = "30m";
}

internal sealed class OllamaChatMessage
{
    [JsonPropertyName("role")]
    public required string Role { get; init; }

    [JsonPropertyName("content")]
    public required string Content { get; init; }
}

internal sealed class OllamaChatOptions
{
    [JsonPropertyName("num_predict")]
    public int NumberOfPredictedTokens { get; init; }
    
    [JsonPropertyName("temperature")]
    public double Temperature { get; init; }
}
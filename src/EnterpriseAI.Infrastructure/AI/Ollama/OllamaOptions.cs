namespace EnterpriseAI.Infrastructure.AI.Ollama;

public sealed class OllamaOptions
{
    public const string SectionName = "Ollama";
    public string BaseUrl { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 300;
    public bool Think { get; set; } = false;
    public int MaximumOutputTokens { get; set; } = 128;
    public double Temperature { get; set; } = 0.1;
}
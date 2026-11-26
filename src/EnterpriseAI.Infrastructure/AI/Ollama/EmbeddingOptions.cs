namespace EnterpriseAI.Infrastructure.AI.Ollama;

public sealed class EmbeddingOptions
{
    public const string SectionName = "Embedding";
    public string Model { get; set; } = string.Empty;
    public int Dimensions { get; set; }
    public int BatchSize { get; set; } = 16;
    public bool Truncate { get; set; } = false;
}
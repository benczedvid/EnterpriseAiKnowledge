namespace EnterpriseAI.Infrastructure.Documents.Chunking;

public sealed class TextChunkingOptions
{
    public const string SectionName = "Chunking";
    public int TargetChunkSize { get; set; } = 1200;
    public int MaximumChunkSize { get; set; } = 1600;
    public int OverlapSize { get; set; } = 200;
    public int MinimumChunkSize { get; set; } = 100;
    public int EstimatedCharactersPerToken { get; set; } = 4;
}
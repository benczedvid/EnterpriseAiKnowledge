namespace EnterpriseAI.Application.Abstractions.Indexing;

public sealed class IndexingOptions
{
    public const string SectionName = "Indexing";

    public int MaximumBatchSize { get; set; } = 100;

    public int DefaultBatchSize { get; set; } = 20;
}
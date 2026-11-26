namespace EnterpriseAI.Domain.Documents;

public enum DocumentProcessingStatus
{
    Discovered = 0,
    Processing = 1,
    Indexed = 2,
    Skipped = 3,
    Failed = 4,
    RequiresOcr = 5
}
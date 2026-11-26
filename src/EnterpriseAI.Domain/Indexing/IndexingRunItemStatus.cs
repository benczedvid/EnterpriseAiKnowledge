namespace EnterpriseAI.Domain.Indexing;

public enum IndexingRunItemStatus
{
    Indexed = 0,
    SkippedUnchanged = 1,
    SkippedUnsupported = 2,
    RequiresOcr = 3,
    Failed = 4,
    NotFound = 5
}
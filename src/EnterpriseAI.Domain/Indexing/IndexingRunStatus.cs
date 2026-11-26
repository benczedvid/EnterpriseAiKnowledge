namespace EnterpriseAI.Domain.Indexing;

public enum IndexingRunStatus
{
    Running = 0,
    Completed = 1,
    CompletedWithErrors = 2,
    Failed = 3,
    Cancelled = 4
}
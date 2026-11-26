namespace EnterpriseAI.Application.Indexing.GetLatestRuns;

public sealed record GetLatestIndexingRunsRequest(
    int Limit = 10);
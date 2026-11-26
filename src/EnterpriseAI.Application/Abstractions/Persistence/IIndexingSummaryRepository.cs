namespace EnterpriseAI.Application.Abstractions.Persistence;

public interface IIndexingSummaryRepository
{
    Task<IndexingSummaryData> GetAsync(
        CancellationToken cancellationToken);
}
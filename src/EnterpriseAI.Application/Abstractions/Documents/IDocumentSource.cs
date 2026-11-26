namespace EnterpriseAI.Application.Abstractions.Documents;

public interface IDocumentSource
{
    Task<IReadOnlyList<SourceDocument>> DiscoverAsync(
        DocumentSourceQuery query,
        CancellationToken cancellationToken);

    Task<SourceDocument?> GetByIdAsync(
        string sourceId,
        CancellationToken cancellationToken);

    Task<Stream> DownloadAsync(
        string sourceId,
        CancellationToken cancellationToken);
}
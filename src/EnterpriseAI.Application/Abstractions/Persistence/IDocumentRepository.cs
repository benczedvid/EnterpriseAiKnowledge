using EnterpriseAI.Domain.Documents;

namespace EnterpriseAI.Application.Abstractions.Persistence;

public interface IDocumentRepository
{
    Task<Document?> GetBySourceAsync(
        string sourceType,
        string sourceId,
        bool includeChunks,
        CancellationToken cancellationToken);

    Task AddAsync(
        Document document,
        CancellationToken cancellationToken);

    void RemoveChunks(
        IReadOnlyCollection<DocumentChunk> chunks);

    Task AddChunksAsync(
        IReadOnlyCollection<DocumentChunk> chunks,
        CancellationToken cancellationToken);

    void Remove(Document document);
}
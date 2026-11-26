using EnterpriseAI.Application.Abstractions.Persistence;
using EnterpriseAI.Domain.Documents;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAI.Infrastructure.Persistence.Repositories;

public sealed class EfDocumentRepository : IDocumentRepository
{
    private readonly EnterpriseAiDbContext _dbContext;

    public EfDocumentRepository(
        EnterpriseAiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Document?> GetBySourceAsync(
        string sourceType,
        string sourceId,
        bool includeChunks,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sourceType))
        {
            throw new ArgumentException(
                "Source type must not be empty.",
                nameof(sourceType));
        }

        if (string.IsNullOrWhiteSpace(sourceId))
        {
            throw new ArgumentException(
                "Source ID must not be empty.",
                nameof(sourceId));
        }

        var normalizedSourceType = sourceType.Trim();
        var normalizedSourceId = sourceId.Trim();

        IQueryable<Document> query =
            _dbContext.Documents;

        if (includeChunks)
        {
            query = query.Include(
                document => document.Chunks);
        }

        return await query.SingleOrDefaultAsync(
            document =>
                document.SourceType == normalizedSourceType
                && document.SourceId == normalizedSourceId,
            cancellationToken);
    }

    public async Task AddAsync(
        Document document,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        await _dbContext.Documents.AddAsync(
            document,
            cancellationToken);
    }

    public void RemoveChunks(
        IReadOnlyCollection<DocumentChunk> chunks)
    {
        ArgumentNullException.ThrowIfNull(chunks);

        if (chunks.Count == 0)
        {
            return;
        }

        _dbContext.DocumentChunks.RemoveRange(chunks);
    }

    public async Task AddChunksAsync(
        IReadOnlyCollection<DocumentChunk> chunks,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(chunks);

        if (chunks.Count == 0)
        {
            throw new ArgumentException(
                "At least one document chunk is required.",
                nameof(chunks));
        }

        await _dbContext.DocumentChunks.AddRangeAsync(
            chunks,
            cancellationToken);
    }

    public void Remove(Document document)
    {
        ArgumentNullException.ThrowIfNull(document);

        _dbContext.Documents.Remove(document);
    }
}
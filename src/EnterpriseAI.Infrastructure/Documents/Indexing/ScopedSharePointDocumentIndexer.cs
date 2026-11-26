using EnterpriseAI.Application.Abstractions.Documents;
using EnterpriseAI.Application.Documents.IndexSharePointDocument;
using Microsoft.Extensions.DependencyInjection;

namespace EnterpriseAI.Infrastructure.Documents.Indexing;

public sealed class ScopedSharePointDocumentIndexer : ISharePointDocumentIndexer
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ScopedSharePointDocumentIndexer(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<IndexSharePointDocumentResponse> IndexAsync(
        IndexSharePointDocumentRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var scope = _scopeFactory.CreateAsyncScope();

        var handler = scope.ServiceProvider.GetRequiredService<IndexSharePointDocumentHandler>();

        return await handler.HandleAsync(request, cancellationToken);
    }
}
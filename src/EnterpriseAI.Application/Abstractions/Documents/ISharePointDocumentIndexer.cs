using EnterpriseAI.Application.Documents.IndexSharePointDocument;

namespace EnterpriseAI.Application.Abstractions.Documents;

public interface ISharePointDocumentIndexer
{
    Task<IndexSharePointDocumentResponse> IndexAsync(
        IndexSharePointDocumentRequest request,
        CancellationToken cancellationToken);
}
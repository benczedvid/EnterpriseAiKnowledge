namespace EnterpriseAI.Application.Abstractions.AI;

public interface IEmbeddingService
{
    Task<EmbeddingResponse> CreateEmbeddingsAsync(
        EmbeddingRequest request,
        CancellationToken cancellationToken);
}
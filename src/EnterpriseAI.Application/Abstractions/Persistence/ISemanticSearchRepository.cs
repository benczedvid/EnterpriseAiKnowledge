namespace EnterpriseAI.Application.Abstractions.Persistence;

public interface ISemanticSearchRepository
{
    Task<IReadOnlyList<SemanticSearchResult>> SearchAsync(
        IReadOnlyList<float> queryEmbedding,
        int limit,
        CancellationToken cancellationToken);
}
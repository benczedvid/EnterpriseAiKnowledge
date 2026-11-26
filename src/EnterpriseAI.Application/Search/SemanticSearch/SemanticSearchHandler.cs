using EnterpriseAI.Application.Abstractions.AI;
using EnterpriseAI.Application.Abstractions.Persistence;

namespace EnterpriseAI.Application.Search.SemanticSearch;

public sealed class SemanticSearchHandler
{
    private const int MaximumQueryLength = 2000;
    private const int MaximumResultLimit = 20;
    private readonly IEmbeddingService _embeddingService;
    private readonly ISemanticSearchRepository _semanticSearchRepository;

    public SemanticSearchHandler(IEmbeddingService embeddingService, ISemanticSearchRepository semanticSearchRepository)
    {
        _embeddingService = embeddingService;
        _semanticSearchRepository = semanticSearchRepository;
    }

    public async Task<SemanticSearchResponse> HandleAsync(SemanticSearchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = GetQuery(request.Query);

        if (request.Limit is < 1 or > MaximumResultLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.Limit),
                $"Limit must be between 1 and {MaximumResultLimit}.");
        }

        var embeddingResponse =
            await _embeddingService.CreateEmbeddingsAsync(
                new EmbeddingRequest([query]),
                cancellationToken);

        if (embeddingResponse.Embeddings.Count != 1)
        {
            throw new InvalidOperationException("The embedding service must return exactly one embedding for the search query.");
        }

        var queryEmbedding = embeddingResponse.Embeddings[0];

        var searchResults = await _semanticSearchRepository.SearchAsync(
                queryEmbedding,
                request.Limit,
                cancellationToken);

        var results = searchResults
            .Select(result =>
                new SemanticSearchItemResponse(
                    DocumentId: result.DocumentId,
                    ChunkId: result.ChunkId,
                    SourceId: result.SourceId,
                    DocumentTitle: result.DocumentTitle,
                    SourceUrl: result.SourceUrl,
                    Content: result.Content,
                    SectionTitle: result.SectionTitle,
                    PageNumber: result.PageNumber,
                    ChunkIndex: result.ChunkIndex,
                    Distance: result.Distance,
                    Similarity: Math.Clamp(
                        1.0 - result.Distance,
                        0.0,
                        1.0)))
            .ToArray();

        return new SemanticSearchResponse(
            Query: query,
            ResultCount: results.Length,
            Results: results);
    }

    private static string GetQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Query must not be empty.", nameof(query));
        }

        var normalizedQuery = query.Trim();

        if (normalizedQuery.Length > MaximumQueryLength)
        {
            throw new ArgumentException($"Query must not exceed {MaximumQueryLength} characters.", nameof(query));
        }

        return normalizedQuery;
    }
}
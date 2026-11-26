namespace EnterpriseAI.Application.Abstractions.AI;

public sealed record EmbeddingResponse(
    IReadOnlyList<IReadOnlyList<float>> Embeddings,
    int Dimensions);
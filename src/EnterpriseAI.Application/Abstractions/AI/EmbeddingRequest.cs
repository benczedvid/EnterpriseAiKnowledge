namespace EnterpriseAI.Application.Abstractions.AI;

public sealed record EmbeddingRequest(
    IReadOnlyList<string> Inputs);
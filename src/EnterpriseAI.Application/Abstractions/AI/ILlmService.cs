namespace EnterpriseAI.Application.Abstractions.AI;

public interface ILlmService
{
    Task<LlmResponse> GenerateAsync(
        LlmRequest request,
        CancellationToken cancellationToken);
}
namespace EnterpriseAI.Application.Abstractions.AI;

public sealed record LlmRequest(
    string SystemPrompt,
    string UserPrompt);
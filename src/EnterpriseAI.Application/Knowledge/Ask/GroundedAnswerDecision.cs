namespace EnterpriseAI.Application.Knowledge.Ask;

public sealed record GroundedAnswerDecision(
    bool CanAnswer,
    string Answer,
    IReadOnlyList<int> UsedSourceNumbers);
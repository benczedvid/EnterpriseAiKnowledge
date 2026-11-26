namespace EnterpriseAI.Application.Knowledge.Ask;

public sealed record AskKnowledgeRequest(
    string Question,
    int RetrievalLimit = 5);
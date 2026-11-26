namespace EnterpriseAI.Application.Search.SemanticSearch;

public sealed record SemanticSearchRequest(
    string Query,
    int Limit = 5);

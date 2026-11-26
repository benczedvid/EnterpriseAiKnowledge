namespace EnterpriseAI.Application.Abstractions.Parsing;

public sealed record ParsedSection(
    string? Heading,
    string Content,
    int? PageNumber,
    int Order);
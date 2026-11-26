using EnterpriseAI.Application.Abstractions.Documents;

namespace EnterpriseAI.Application.Abstractions.Parsing;

public sealed record DocumentParseRequest(
    SourceDocument SourceDocument,
    Stream Content);
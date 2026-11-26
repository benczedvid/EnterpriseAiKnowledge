using EnterpriseAI.Application.Abstractions.Documents;

namespace EnterpriseAI.Application.Abstractions.Parsing;

public interface IDocumentParserResolver
{
    IDocumentParser? Resolve(SourceDocument sourceDocument);
}
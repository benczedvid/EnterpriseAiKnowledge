namespace EnterpriseAI.Application.Abstractions.Parsing;

public interface IDocumentParser
{
    bool CanParse(string fileName, string mimeType);

    Task<DocumentParseResult> ParseAsync(
        DocumentParseRequest request,
        CancellationToken cancellationToken);
}
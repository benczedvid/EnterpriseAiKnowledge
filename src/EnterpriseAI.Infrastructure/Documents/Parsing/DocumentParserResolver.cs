using EnterpriseAI.Application.Abstractions.Documents;
using EnterpriseAI.Application.Abstractions.Parsing;

namespace EnterpriseAI.Infrastructure.Documents.Parsing;

public sealed class DocumentParserResolver : IDocumentParserResolver
{
    private readonly IReadOnlyList<IDocumentParser> _parsers;

    public DocumentParserResolver(IEnumerable<IDocumentParser> parsers)
    {
        ArgumentNullException.ThrowIfNull(parsers);

        _parsers = parsers.ToArray();
    }

    public IDocumentParser? Resolve(SourceDocument sourceDocument)
    {
        ArgumentNullException.ThrowIfNull(sourceDocument);

        var matchingParsers = _parsers
            .Where(parser =>
                parser.CanParse(
                    sourceDocument.FileName,
                    sourceDocument.MimeType))
            .ToArray();

        return matchingParsers.Length switch
        {
            0 => null,

            1 => matchingParsers[0],

            _ => throw new InvalidOperationException(
                $"Multiple document parsers can handle " +
                $"'{sourceDocument.FileName}' with MIME type " +
                $"'{sourceDocument.MimeType}'.")
        };
    }
}
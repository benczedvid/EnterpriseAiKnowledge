using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using EnterpriseAI.Application.Abstractions.Parsing;

namespace EnterpriseAI.Infrastructure.Documents.Parsing.Docx;

public sealed class DocxDocumentParser : IDocumentParser
{
    private const string DocxExtension = ".docx";

    private const string DocxMimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private const int MinimumUsefulCharacterCount = 20;

    private static readonly Regex WhitespaceRegex =
        new(
            @"[^\S\r\n]+",
            RegexOptions.CultureInvariant |
            RegexOptions.Compiled);

    private static readonly Regex ExcessiveLineBreakRegex =
        new(
            @"\n{3,}",
            RegexOptions.CultureInvariant |
            RegexOptions.Compiled);

    public bool CanParse(string fileName, string mimeType)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        var extension = Path.GetExtension(fileName);

        if (string.Equals(extension, DocxExtension, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(
            mimeType?.Trim(),
            DocxMimeType,
            StringComparison.OrdinalIgnoreCase);
    }

    public async Task<DocumentParseResult> ParseAsync(
        DocumentParseRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.SourceDocument);
        ArgumentNullException.ThrowIfNull(request.Content);

        cancellationToken.ThrowIfCancellationRequested();

        if (!CanParse(request.SourceDocument.FileName, request.SourceDocument.MimeType))
        {
            return DocumentParseResult.Unsupported($"The file '{request.SourceDocument.FileName}' is not a supported DOCX document.");
        }

        if (!request.Content.CanRead)
        {
            throw new ArgumentException("DOCX content stream must be readable.", nameof(request));
        }

        await using var docxContent = new MemoryStream();

        await request.Content.CopyToAsync(docxContent, cancellationToken);

        if (docxContent.Length == 0)
        {
            throw new InvalidOperationException("DOCX content stream is empty.");
        }

        docxContent.Position = 0;

        try
        {
            using var document = WordprocessingDocument.Open(docxContent, isEditable: false);
            var body = document.MainDocumentPart?.Document?.Body;

            if (body is null)
            {
                throw new InvalidOperationException("The DOCX document does not contain a readable document body.");
            }

            var sections = ParseBody(body, cancellationToken);
            var usefulCharacterCount = sections.Sum(
                section => CountUsefulCharacters(section.Content));

            if (usefulCharacterCount < MinimumUsefulCharacterCount)
            {
                return DocumentParseResult.Unsupported(
                    "The DOCX document contains no usable text or too little extractable content.");
            }

            var parsedDocument = new ParsedDocument(
                Title: request.SourceDocument.Title,
                Sections: sections);

            return DocumentParseResult.Success(parsedDocument);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            throw new InvalidOperationException(
                $"The DOCX document '{request.SourceDocument.FileName}' could not be parsed.", exception);
        }
    }

    private static IReadOnlyList<ParsedSection> ParseBody(Body body, CancellationToken cancellationToken)
    {
        var sections = new List<ParsedSection>();

        string? currentHeading = null;
        var currentContent = new StringBuilder();

        foreach (var element in body.ChildElements)
        {
            cancellationToken.ThrowIfCancellationRequested();

            switch (element)
            {
                case Paragraph paragraph:
                {
                    var paragraphText = ExtractParagraphText(paragraph);

                    if (string.IsNullOrWhiteSpace(paragraphText))
                    {
                        continue;
                    }

                    if (IsHeading(paragraph))
                    {
                        AddSection(
                            sections,
                            currentHeading,
                            currentContent);

                        currentHeading = paragraphText;
                        currentContent.Clear();
                    }
                    else
                    {
                        AppendContent(currentContent, paragraphText);
                    }

                    break;
                }

                case Table table:
                {
                    var tableText = ExtractTableText(table);

                    if (!string.IsNullOrWhiteSpace(tableText))
                    {
                        AppendContent(currentContent, tableText);
                    }

                    break;
                }
            }
        }

        AddSection(sections, currentHeading, currentContent);

        return sections;
    }

    private static void AddSection(
        ICollection<ParsedSection> sections,
        string? heading,
        StringBuilder content)
    {
        var normalizedContent = NormalizeText(content.ToString());

        if (string.IsNullOrWhiteSpace(normalizedContent))
        {
            return;
        }

        sections.Add(
            new ParsedSection(
                Heading: NormalizeOptionalText(heading),
                Content: normalizedContent,
                PageNumber: null,
                Order: sections.Count));
    }

    private static void AppendContent(StringBuilder builder, string content)
    {
        var normalizedContent = NormalizeText(content);

        if (string.IsNullOrWhiteSpace(normalizedContent))
        {
            return;
        }

        if (builder.Length > 0)
        {
            builder.AppendLine();
            builder.AppendLine();
        }

        builder.Append(normalizedContent);
    }

    private static string ExtractParagraphText(Paragraph paragraph)
    {
        var builder = new StringBuilder();

        foreach (var element in paragraph.Descendants())
        {
            switch (element)
            {
                case Text text:
                    builder.Append(text.Text);
                    break;

                case TabChar:
                    builder.Append('\t');
                    break;

                case Break:
                case CarriageReturn:
                    builder.AppendLine();
                    break;
            }
        }

        return NormalizeText(builder.ToString());
    }

    private static bool IsHeading(Paragraph paragraph)
    {
        var styleId = paragraph
            .ParagraphProperties?
            .ParagraphStyleId?
            .Val?
            .Value;

        if (string.IsNullOrWhiteSpace(styleId))
        {
            return false;
        }

        var normalizedStyleId = styleId.Trim();

        return normalizedStyleId.StartsWith(
                   "Heading",
                   StringComparison.OrdinalIgnoreCase)
               || normalizedStyleId.StartsWith(
                   "Címsor",
                   StringComparison.OrdinalIgnoreCase)
               || normalizedStyleId.StartsWith(
                   "Cimsor",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractTableText(Table table)
    {
        var rows = new List<string>();

        foreach (var row in table.Elements<TableRow>())
        {
            var cells = row
                .Elements<TableCell>()
                .Select(cell =>
                    NormalizeText(
                        string.Join(
                            " ",
                            cell
                                .Elements<Paragraph>()
                                .Select(ExtractParagraphText)
                                .Where(text =>
                                    !string.IsNullOrWhiteSpace(text)))))
                .Where(text =>
                    !string.IsNullOrWhiteSpace(text))
                .ToArray();

            if (cells.Length == 0)
            {
                continue;
            }

            rows.Add(string.Join(" | ", cells));
        }

        return string.Join(Environment.NewLine, rows);
    }

    private static string NormalizeText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Replace('\u00A0', ' ');

        normalized = WhitespaceRegex.Replace(normalized, " ");
        normalized = ExcessiveLineBreakRegex.Replace(normalized, "\n\n");

        return normalized.Trim();
    }

    private static string? NormalizeOptionalText(string? text)
    {
        var normalized = NormalizeText(text);

        return string.IsNullOrWhiteSpace(normalized)
            ? null
            : normalized;
    }

    private static int CountUsefulCharacters(string text)
    {
        return text.Count(character => char.IsLetterOrDigit(character));
    }
}
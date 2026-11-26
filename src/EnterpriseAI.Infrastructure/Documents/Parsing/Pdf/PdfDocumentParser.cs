using System.Text.RegularExpressions;
using EnterpriseAI.Application.Abstractions.Parsing;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace EnterpriseAI.Infrastructure.Documents.Parsing.Pdf;

public sealed class PdfDocumentParser : IDocumentParser
{
    private const string PdfExtension = ".pdf";
    private const string PdfMimeType = "application/pdf";
    private static readonly Regex HorizontalWhitespaceRegex =
    new(
        @"[^\S\r\n]+",
        RegexOptions.CultureInvariant |
        RegexOptions.Compiled);

    private static readonly Regex SpaceBeforeLineBreakRegex =
        new(
            @"[ \t]+\n",
            RegexOptions.CultureInvariant |
            RegexOptions.Compiled);

    private static readonly Regex ExcessiveLineBreakRegex =
        new(
            @"\n{3,}",
            RegexOptions.CultureInvariant |
            RegexOptions.Compiled);

    private const int MinimumUsefulCharacterCount = 20;

    public bool CanParse(string fileName, string mimeType)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        var extension = Path.GetExtension(fileName);

        if (string.Equals(extension, PdfExtension, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(mimeType?.Trim(), PdfMimeType, StringComparison.OrdinalIgnoreCase);
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
            return DocumentParseResult.Unsupported(
                $"The file '{request.SourceDocument.FileName}' is not a supported PDF document.");
        }

        if (!request.Content.CanRead)
        {
            throw new ArgumentException("PDF content stream must be readable.", nameof(request));
        }

        await using var pdfContent = new MemoryStream();

        await request.Content.CopyToAsync(pdfContent, cancellationToken);

        if (pdfContent.Length == 0)
        {
            throw new InvalidOperationException("PDF content stream is empty.");
        }

        pdfContent.Position = 0;

        try
        {
            using var pdfDocument = PdfDocument.Open(pdfContent);

            var sections = new List<ParsedSection>();

            foreach (var page in pdfDocument.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var extractedText = ContentOrderTextExtractor.GetText(page);
                var normalizedText = NormalizeText(extractedText);

                if (string.IsNullOrWhiteSpace(normalizedText))
                {
                    continue;
                }

                sections.Add(
                    new ParsedSection(
                        Heading: null,
                        Content: normalizedText,
                        PageNumber: page.Number,
                        Order: sections.Count));
            }

            var usefulCharacterCount = sections.Sum(section => CountUsefulCharacters(section.Content));

            if (usefulCharacterCount <
                MinimumUsefulCharacterCount)
            {
                return DocumentParseResult.RequiresOcr(
                    "The PDF contains no usable text layer or too little extractable text. OCR processing is required.");
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
                $"The PDF document '{request.SourceDocument.FileName}' could not be parsed.", exception);
        }
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

        normalized = HorizontalWhitespaceRegex .Replace(normalized, " ");
        normalized = SpaceBeforeLineBreakRegex.Replace(normalized, "\n");
        normalized = ExcessiveLineBreakRegex.Replace(normalized, "\n\n");

        return normalized.Trim();
    }

    private static int CountUsefulCharacters(string text)
    {
        return text.Count(character => char.IsLetterOrDigit(character));
    }
}
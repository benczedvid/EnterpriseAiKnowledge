namespace EnterpriseAI.Application.Abstractions.Parsing;

public sealed record DocumentParseResult(
    DocumentParseStatus Status,
    ParsedDocument? Document,
    string? Message)
{
    public static DocumentParseResult Success(ParsedDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (!document.HasContent)
        {
            throw new ArgumentException("Parsed document must contain text.", nameof(document));
        }

        return new DocumentParseResult(
            Status: DocumentParseStatus.Success,
            Document: document,
            Message: null);
    }

    public static DocumentParseResult RequiresOcr(string message)
    {
        return new DocumentParseResult(
            Status: DocumentParseStatus.RequiresOcr,
            Document: null,
            Message: GetRequiredMessage(message));
    }

    public static DocumentParseResult Unsupported(string message)
    {
        return new DocumentParseResult(
            Status: DocumentParseStatus.Unsupported,
            Document: null,
            Message: GetRequiredMessage(message));
    }

    private static string GetRequiredMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Message must not be empty.", nameof(message));
        }

        return message.Trim();
    }
}
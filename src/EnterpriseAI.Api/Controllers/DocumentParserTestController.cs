using EnterpriseAI.Application.Abstractions.Chunking;
using EnterpriseAI.Application.Abstractions.Documents;
using EnterpriseAI.Application.Abstractions.Parsing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Graph.Models.ODataErrors;

namespace EnterpriseAI.Api.Controllers;

[ApiController]
[Route("api/dev/document-parser")]
public sealed class DocumentParserTestController : ControllerBase
{
    private const int MaximumPreviewLength = 300;

    private readonly IDocumentSource _documentSource;
    private readonly IDocumentParserResolver _parserResolver;
    private readonly IWebHostEnvironment _environment;
    private readonly IChunkingService _chunkingService;


    public DocumentParserTestController(
        IDocumentSource documentSource,
        IDocumentParserResolver parserResolver,
        IWebHostEnvironment environment,
        IChunkingService chunkingService)
    {
        _documentSource = documentSource;
        _parserResolver = parserResolver;
        _environment = environment;
        _chunkingService = chunkingService;
    }

    [HttpPost("sharepoint")]
    public async Task<ActionResult<DocumentParserTestResponse>>ParseSharePointDocumentAsync(
            [FromBody] DocumentParserTestRequest request,
            CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.SourceId))
        {
            ModelState.AddModelError(nameof(request.SourceId), "Source ID must not be empty.");

            return ValidationProblem(ModelState);
        }

        try
        {
            var discoveryQuery = new DocumentSourceQuery(
                FolderPath: null,
                AllowedExtensions:
                [
                    ".pdf",
                    ".docx"
                ],
                MaximumDocuments: 100);

            var documents = await _documentSource.DiscoverAsync(discoveryQuery, cancellationToken);
            var sourceDocument = documents.SingleOrDefault(
                document =>
                    string.Equals(
                        document.SourceId,
                        request.SourceId.Trim(),
                        StringComparison.Ordinal));

            if (sourceDocument is null)
            {
                return NotFound(new
                {
                    message = "The requested SharePoint document was not found in the current discovery result."
                });
            }

            var parser = _parserResolver.Resolve(sourceDocument);

            if (parser is null)
            {
                return UnprocessableEntity(new
                {
                    message = "No document parser is registered for the requested file type."
                });
            }

            await using var content = await _documentSource.DownloadAsync(sourceDocument.SourceId, cancellationToken);

            var parseResult = await parser.ParseAsync(new DocumentParseRequest(
                    SourceDocument: sourceDocument,
                    Content: content),
                cancellationToken);

            ChunkingResult? chunkingResult = null;

            if (parseResult.Status == DocumentParseStatus.Success && parseResult.Document is not null)
            {
                chunkingResult = _chunkingService.CreateChunks(new ChunkingRequest(parseResult.Document));
            }

            return Ok(
                CreateResponse(
                    sourceDocument,
                    parseResult,
                    chunkingResult));
        }
        catch (ODataError exception)
        {
            return Problem(
                statusCode: exception.ResponseStatusCode switch
                {
                    StatusCodes.Status403Forbidden => StatusCodes.Status403Forbidden,
                    StatusCodes.Status404NotFound => StatusCodes.Status404NotFound,
                    StatusCodes.Status429TooManyRequests => StatusCodes.Status503ServiceUnavailable,

                    _ => StatusCodes.Status502BadGateway
                },
                title: "SharePoint document parsing failed.",
                detail: "Microsoft Graph returned an unsuccessful response.");
        }
        catch (InvalidOperationException)
        {
            return Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Document parsing failed.",
                detail: "The document could not be parsed.");
        }
    }

    private static DocumentParserTestResponse CreateResponse(
        SourceDocument sourceDocument,
        DocumentParseResult parseResult,
        ChunkingResult? chunkingResult)
    {
        var sections = parseResult.Document?.Sections
            .Select(section =>
                new ParsedSectionPreviewResponse(
                    Heading: section.Heading,
                    PageNumber: section.PageNumber,
                    Order: section.Order,
                    CharacterCount: section.Content.Length,
                    Preview: CreatePreview(section.Content)))
            .ToArray()
            ?? [];

        var chunks = chunkingResult?.Chunks
            .Select(chunk =>
                new TextChunkPreviewResponse(
                    ChunkIndex: chunk.ChunkIndex,
                    SectionTitle: chunk.SectionTitle,
                    PageNumber: chunk.PageNumber,
                    CharacterCount: chunk.Content.Length,
                    EstimatedTokenCount: chunk.EstimatedTokenCount,
                    Preview: CreatePreview(chunk.Content)))
            .ToArray()
            ?? [];

        return new DocumentParserTestResponse(
            SourceId: sourceDocument.SourceId,
            FileName: sourceDocument.FileName,
            ParseStatus: parseResult.Status.ToString(),
            Message: parseResult.Message,
            Title: parseResult.Document?.Title,
            SectionCount: sections.Length,
            CharacterCount: parseResult.Document?.CharacterCount ?? 0,
            Sections: sections,
            ChunkCount: chunks.Length,
            EstimatedTotalTokenCount: chunkingResult?.EstimatedTotalTokenCount ?? 0,
            Chunks: chunks);
    }

    private static string CreatePreview(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var normalized = content
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();

        return normalized.Length <= MaximumPreviewLength
            ? normalized
            : $"{normalized[..MaximumPreviewLength]}...";
    }
}

public sealed class DocumentParserTestRequest
{
    public string SourceId { get; init; } = string.Empty;
}

public sealed record DocumentParserTestResponse(
    string SourceId,
    string FileName,
    string ParseStatus,
    string? Message,
    string? Title,
    int SectionCount,
    int CharacterCount,
    IReadOnlyList<ParsedSectionPreviewResponse> Sections,
    int ChunkCount,
    int EstimatedTotalTokenCount,
    IReadOnlyList<TextChunkPreviewResponse> Chunks);

public sealed record ParsedSectionPreviewResponse(
    string? Heading,
    int? PageNumber,
    int Order,
    int CharacterCount,
    string Preview);

public sealed record TextChunkPreviewResponse(
    int ChunkIndex,
    string? SectionTitle,
    int? PageNumber,
    int CharacterCount,
    int EstimatedTokenCount,
    string Preview);
using EnterpriseAI.Application.Abstractions.Documents;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Graph.Models.ODataErrors;

namespace EnterpriseAI.Api.Controllers;

[ApiController]
[Route("api/dev/sharepoint/discovery")]
public sealed class SharePointDiscoveryController : ControllerBase
{
    private readonly IDocumentSource _documentSource;
    private readonly IWebHostEnvironment _environment;

    public SharePointDiscoveryController(
        IDocumentSource documentSource,
        IWebHostEnvironment environment)
    {
        _documentSource = documentSource;
        _environment = environment;
    }

    [HttpGet]
    public async Task<ActionResult<SharePointDiscoveryResponse>> DiscoverAsync(
            [FromQuery] int maximumDocuments = 500,
            CancellationToken cancellationToken = default)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

    if (maximumDocuments is < 1 or > 500)
    {
        ModelState.AddModelError(
            nameof(maximumDocuments),
            "Maximum documents must be between 1 and 100.");

        return ValidationProblem(ModelState);
    }

        var query = new DocumentSourceQuery(
            FolderPath: null,
            AllowedExtensions:
            [
                ".pdf",
                ".docx",
                ".xlsx"
            ],
            MaximumDocuments: maximumDocuments);

        try
        {
            var documents = await _documentSource.DiscoverAsync(query, cancellationToken);

            var response = new SharePointDiscoveryResponse(
                Count: documents.Count,
                Documents: documents
                    .Select(document =>
                        new SharePointDiscoveredDocumentResponse(
                            SourceId: document.SourceId,
                            FileName: document.FileName,
                            Title: document.Title,
                            MimeType: document.MimeType,
                            FolderPath: document.FolderPath,
                            ETag: document.ETag,
                            SizeInBytes: document.SizeInBytes,
                            SourceCreatedAt: document.SourceCreatedAt,
                            SourceModifiedAt: document.SourceModifiedAt,
                            SourceUrl: document.SourceUrl))
                    .ToArray());

            return Ok(response);
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
                title: "SharePoint discovery failed.",
                detail: exception.ResponseStatusCode switch
                {
                    StatusCodes.Status403Forbidden =>
                        "The application does not have read access to the configured document library.",

                    StatusCodes.Status404NotFound =>
                        "The configured document library or root item could not be found.",

                    StatusCodes.Status429TooManyRequests =>
                        "Microsoft Graph temporarily throttled the discovery request.",

                    _ =>
                        "Microsoft Graph returned an unsuccessful response."
                });
        }
        catch (Azure.Identity.AuthenticationFailedException)
        {
            return Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "SharePoint authentication failed.",
                detail: "The configured Entra ID application could not authenticate.");
        }
    }
}

public sealed record SharePointDiscoveryResponse(
    int Count,
    IReadOnlyList<SharePointDiscoveredDocumentResponse> Documents);

public sealed record SharePointDiscoveredDocumentResponse(
    string SourceId,
    string FileName,
    string Title,
    string MimeType,
    string? FolderPath,
    string? ETag,
    long SizeInBytes,
    DateTimeOffset SourceCreatedAt,
    DateTimeOffset SourceModifiedAt,
    string SourceUrl);
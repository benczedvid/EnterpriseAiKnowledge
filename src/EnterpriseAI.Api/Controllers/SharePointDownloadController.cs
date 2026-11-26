using EnterpriseAI.Application.Abstractions.Documents;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Graph.Models.ODataErrors;

namespace EnterpriseAI.Api.Controllers;

[ApiController]
[Route("api/dev/sharepoint/download")]
public sealed class SharePointDownloadController : ControllerBase
{
    private const int PreviewByteCount = 16;

    private readonly IDocumentSource _documentSource;
    private readonly IWebHostEnvironment _environment;

    public SharePointDownloadController(
        IDocumentSource documentSource,
        IWebHostEnvironment environment)
    {
        _documentSource = documentSource;
        _environment = environment;
    }

    [HttpGet("{sourceId}")]
    public async Task<ActionResult<SharePointDownloadTestResponse>>DownloadAsync(string sourceId, CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(sourceId))
        {
            ModelState.AddModelError(nameof(sourceId), "Source ID must not be empty.");

            return ValidationProblem(ModelState);
        }

        try
        {
            await using var sourceStream = await _documentSource.DownloadAsync(sourceId, cancellationToken);

            await using var memoryStream = new MemoryStream();

            await sourceStream.CopyToAsync(memoryStream, cancellationToken);

            var content = memoryStream.ToArray();

            if (content.Length == 0)
            {
                return Problem(
                    statusCode: StatusCodes.Status502BadGateway,
                    title: "Downloaded file is empty.",
                    detail: "Microsoft Graph returned an empty file stream.");
            }

            var previewBytes = content
                .Take(PreviewByteCount)
                .ToArray();

            return Ok(
                new SharePointDownloadTestResponse(
                    SourceId: sourceId.Trim(),
                    ContentLength: content.LongLength,
                    FirstBytesBase64: Convert.ToBase64String(previewBytes),
                    DetectedFileType: DetectFileType(previewBytes)));
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
                title: "SharePoint download failed.",
                detail: exception.ResponseStatusCode switch
                {
                    StatusCodes.Status403Forbidden =>
                        "The application does not have permission to download the requested file.",

                    StatusCodes.Status404NotFound =>
                        "The requested SharePoint file was not found.",

                    StatusCodes.Status429TooManyRequests =>
                        "Microsoft Graph temporarily throttled the download request.",

                    _ =>
                        "Microsoft Graph returned an unsuccessful download response."
                });
        }
        catch (InvalidOperationException exception)
        {
            return Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "SharePoint file cannot be processed.",
                detail: exception.Message);
        }
    }

    private static string DetectFileType(
        IReadOnlyList<byte> bytes)
    {
        if (bytes.Count >= 5
            && bytes[0] == 0x25
            && bytes[1] == 0x50
            && bytes[2] == 0x44
            && bytes[3] == 0x46
            && bytes[4] == 0x2D)
        {
            return "PDF";
        }

        if (bytes.Count >= 4
            && bytes[0] == 0x50
            && bytes[1] == 0x4B
            && bytes[2] == 0x03
            && bytes[3] == 0x04)
        {
            return "ZIP-based Office document";
        }

        return "Unknown";
    }
}

public sealed record SharePointDownloadTestResponse(
    string SourceId,
    long ContentLength,
    string FirstBytesBase64,
    string DetectedFileType);
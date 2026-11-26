using System.ComponentModel.DataAnnotations;
using EnterpriseAI.Application.Documents.IndexSharePointDocument;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Graph.Models.ODataErrors;

namespace EnterpriseAI.Api.Controllers;

[ApiController]
[Route("api/dev/sharepoint/index")]
public sealed class SharePointIndexController : ControllerBase
{
    private readonly IndexSharePointDocumentHandler _handler;
    private readonly IWebHostEnvironment _environment;

    public SharePointIndexController(
        IndexSharePointDocumentHandler handler,
        IWebHostEnvironment environment)
    {
        _handler = handler;
        _environment = environment;
    }

    [HttpPost]
    public async Task<ActionResult<IndexSharePointDocumentApiResponse>>IndexAsync(
            [FromBody]
            IndexSharePointDocumentApiRequest request,
            CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        try
        {
            var response = await _handler.HandleAsync(
                new IndexSharePointDocumentRequest(
                    SourceId: request.SourceId,
                    ForceReindex: request.ForceReindex),
                cancellationToken);

            var apiResponse = IndexSharePointDocumentApiResponse.From(response);

            return response.Status switch
            {
                IndexSharePointDocumentStatus.NotFound => NotFound(apiResponse),
                IndexSharePointDocumentStatus.Failed => UnprocessableEntity(apiResponse),

                _ => Ok(apiResponse)
            };
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError("request", exception.Message);

            return ValidationProblem(ModelState);
        }
        catch (ODataError exception)
        {
            return Problem(
                statusCode: MapGraphStatusCode(exception.ResponseStatusCode),
                title: "SharePoint indexing request failed.",
                detail: GetSafeGraphErrorMessage(exception.ResponseStatusCode));
        }
        catch (Azure.Identity.AuthenticationFailedException)
        {
            return Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "SharePoint authentication failed.",
                detail: "The configured Entra ID application could not authenticate with Microsoft Graph.");
        }
        catch (HttpRequestException)
        {
            return Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "AI service request failed.",
                detail: "The embedding service could not be reached or returned an unsuccessful response.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Problem(
                statusCode: StatusCodes.Status504GatewayTimeout,
                title: "Indexing request timed out.",
                detail: "The indexing operation did not complete within the configured timeout.");
        }
    }

    private static int MapGraphStatusCode(int statusCode)
    {
        return statusCode switch
        {
            StatusCodes.Status403Forbidden => StatusCodes.Status403Forbidden,
            StatusCodes.Status404NotFound => StatusCodes.Status404NotFound,
            StatusCodes.Status429TooManyRequests => StatusCodes.Status503ServiceUnavailable,

            _ => StatusCodes.Status502BadGateway
        };
    }

    private static string GetSafeGraphErrorMessage(int statusCode)
    {
        return statusCode switch
        {
            StatusCodes.Status403Forbidden =>
                "The application does not have read access to the configured SharePoint resource.",

            StatusCodes.Status404NotFound =>
                "The requested SharePoint resource could not be found.",

            StatusCodes.Status429TooManyRequests =>
                "Microsoft Graph temporarily throttled the indexing request.",

            _ =>
                "Microsoft Graph returned an unsuccessful response."
        };
    }
}

public sealed class IndexSharePointDocumentApiRequest
{
    [Required]
    [StringLength(500, MinimumLength = 1)]
    public string SourceId { get; init; } = string.Empty;

    public bool ForceReindex { get; init; }
}

public sealed record IndexSharePointDocumentApiResponse(
    string SourceId,
    string? FileName,
    string Status,
    string Message,
    Guid? DocumentId,
    int ChunkCount,
    int CharacterCount,
    DateTimeOffset? IndexedAt,
    bool Succeeded)
{
    public static IndexSharePointDocumentApiResponse From(IndexSharePointDocumentResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return new IndexSharePointDocumentApiResponse(
            SourceId: response.SourceId,
            FileName: response.FileName,
            Status: response.Status.ToString(),
            Message: response.Message,
            DocumentId: response.DocumentId,
            ChunkCount: response.ChunkCount,
            CharacterCount: response.CharacterCount,
            IndexedAt: response.IndexedAt,
            Succeeded: response.Succeeded);
    }
}
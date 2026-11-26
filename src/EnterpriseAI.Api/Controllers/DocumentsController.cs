using EnterpriseAI.Application.Documents.GetBySource;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseAI.Api.Controllers;

[ApiController]
[Route("api/documents")]
public sealed class DocumentsController : ControllerBase
{
    private readonly GetDocumentBySourceHandler _handler;

    public DocumentsController(GetDocumentBySourceHandler handler)
    {
        _handler = handler;
    }

    [HttpGet("by-source")]
    public async Task<ActionResult<DocumentDetailsResponse>> GetBySourceAsync(
            [FromQuery] string sourceType,
            [FromQuery] string sourceId,
            [FromQuery] bool includeChunks = false,
            CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new GetDocumentBySourceRequest(
                SourceType: sourceType,
                SourceId: sourceId,
                IncludeChunks: includeChunks);

            var response = await _handler.HandleAsync(request, cancellationToken);

            if (!response.Found || response.Document is null)
            {
                return NotFound(new
                {
                    message = "The requested document was not found."
                });
            }

            return Ok(response.Document);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError("request", exception.Message);

            return ValidationProblem(ModelState);
        }
    }
}
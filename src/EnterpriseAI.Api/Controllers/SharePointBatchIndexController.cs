using System.ComponentModel.DataAnnotations;
using EnterpriseAI.Application.Documents.IndexSharePointBatch;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseAI.Api.Controllers;

[ApiController]
[Route("api/dev/sharepoint/index-batch")]
public sealed class SharePointBatchIndexController : ControllerBase
{
    private readonly StartSharePointBatchHandler _startHandler;
    private readonly IWebHostEnvironment _environment;

    public SharePointBatchIndexController(
        StartSharePointBatchHandler startHandler,
        IWebHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(startHandler);
        ArgumentNullException.ThrowIfNull(environment);

        _startHandler = startHandler;
        _environment = environment;
    }

    [HttpPost]
    [ProducesResponseType(typeof(StartSharePointBatchApiResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<StartSharePointBatchApiResponse>> IndexBatchAsync(
            [FromBody]IndexSharePointBatchApiRequest request,
            CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        try
        {
            var indexingRunId = await _startHandler.HandleAsync(
                    new IndexSharePointBatchRequest(
                        FolderPath: request.FolderPath,
                        MaximumDocuments: request.MaximumDocuments,
                        ForceReindex: request.ForceReindex),
                    cancellationToken);

            var response = new StartSharePointBatchApiResponse(
                    IndexingRunId: indexingRunId,
                    Status: "Queued",
                    StatusUrl: $"/api/indexing-runs/{indexingRunId}");

            return Accepted(response.StatusUrl, response);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(nameof(request), exception.Message);

            return ValidationProblem(ModelState);
        }
        catch (InvalidOperationException)
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "The indexing job could not be queued.",
                detail: "The background indexing queue is temporarily unavailable.");
        }
    }
}

public sealed class IndexSharePointBatchApiRequest
{
    public string? FolderPath { get; init; }
    [Range(1, 500)]
    public int MaximumDocuments { get; init; } = 60;
    public bool ForceReindex { get; init; }
}

public sealed record StartSharePointBatchApiResponse(
    Guid IndexingRunId,
    string Status,
    string StatusUrl);
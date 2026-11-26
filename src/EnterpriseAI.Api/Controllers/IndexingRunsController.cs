using EnterpriseAI.Application.Indexing.GetLatestRuns;
using EnterpriseAI.Application.Indexing.GetRunById;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseAI.Api.Controllers;

[ApiController]
[Route("api/indexing-runs")]
public sealed class IndexingRunsController : ControllerBase
{
    private readonly GetLatestIndexingRunsHandler _getLatestHandler;
    private readonly GetIndexingRunByIdHandler _getByIdHandler;

    public IndexingRunsController(
        GetLatestIndexingRunsHandler getLatestHandler,
        GetIndexingRunByIdHandler getIndexingRunByIdHandler)
    {
        _getLatestHandler = getLatestHandler;
        _getByIdHandler = getIndexingRunByIdHandler;
    }

    [HttpGet]
    public async Task<ActionResult<GetLatestIndexingRunsResponse>>GetLatestAsync(
            [FromQuery] int limit = 10,
            CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _getLatestHandler.HandleAsync(
                    new GetLatestIndexingRunsRequest(limit),
                    cancellationToken);

            return Ok(response);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(nameof(limit), exception.Message);

            return ValidationProblem(ModelState);
        }
    }

    [HttpGet("{indexingRunId:guid}")]
    public async Task<ActionResult<IndexingRunDetailsResponse>>GetByIdAsync(
            Guid indexingRunId,
            CancellationToken cancellationToken)
    {
        if (indexingRunId == Guid.Empty)
        {
            ModelState.AddModelError(
                nameof(indexingRunId),
                "Indexing run ID must not be empty.");

            return ValidationProblem(ModelState);
        }

        var response = await _getByIdHandler.HandleAsync(
            new GetIndexingRunByIdRequest(indexingRunId),
            cancellationToken);

        if (!response.Found || response.Run is null)
        {
            return NotFound(new
            {
                message = "The requested indexing run was not found."
            });
        }

        return Ok(response.Run);
    }
}
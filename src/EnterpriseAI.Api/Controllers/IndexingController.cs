using EnterpriseAI.Application.Indexing.GetSummary;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseAI.Api.Controllers;

[ApiController]
[Route("api/indexing")]
public sealed class IndexingController : ControllerBase
{
    private readonly GetIndexingSummaryHandler _summaryHandler;

    public IndexingController(GetIndexingSummaryHandler summaryHandler)
    {
        _summaryHandler = summaryHandler;
    }

    [HttpGet("summary")]
    public async Task<
        ActionResult<GetIndexingSummaryResponse>>
        GetSummaryAsync(CancellationToken cancellationToken)
    {
        var response = await _summaryHandler.HandleAsync(cancellationToken);

        return Ok(response);
    }
}
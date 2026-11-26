using System.ComponentModel.DataAnnotations;
using EnterpriseAI.Application.Search.SemanticSearch;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseAI.Api.Controllers;

[ApiController]
[Route("api/search")]
public sealed class SearchController : ControllerBase
{
    private readonly SemanticSearchHandler _semanticSearchHandler;

    public SearchController(SemanticSearchHandler semanticSearchHandler)
    {
        _semanticSearchHandler = semanticSearchHandler;
    }

    [HttpPost("semantic")]
    public async Task<ActionResult<SemanticSearchResponse>>SemanticSearchAsync(
            [FromBody] SemanticSearchApiRequest request,
            CancellationToken cancellationToken)
    {
        try
        {
            var applicationRequest = new SemanticSearchRequest(
                    Query: request.Query,
                    Limit: request.Limit);

            var response = await _semanticSearchHandler.HandleAsync(
                    applicationRequest,
                    cancellationToken);

            return Ok(response);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError("request", exception.Message);

            return ValidationProblem(ModelState);
        }
    }
}

public sealed class SemanticSearchApiRequest
{
    [Required]
    [StringLength(2000, MinimumLength = 1)]
    public string Query { get; init; } = string.Empty;

    [Range(1, 20)]
    public int Limit { get; init; } = 5;
}
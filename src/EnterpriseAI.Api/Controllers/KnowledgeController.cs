using System.ComponentModel.DataAnnotations;
using EnterpriseAI.Application.Knowledge.Ask;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseAI.Api.Controllers;

[ApiController]
[Route("api/knowledge")]
public sealed class KnowledgeController : ControllerBase
{
    private readonly AskKnowledgeHandler _askKnowledgeHandler;

    public KnowledgeController(AskKnowledgeHandler askKnowledgeHandler)
    {
        _askKnowledgeHandler = askKnowledgeHandler;
    }

    [HttpPost("ask")]
    public async Task<ActionResult<AskKnowledgeResponse>> AskAsync(
        [FromBody] AskKnowledgeApiRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var applicationRequest = new AskKnowledgeRequest(
                Question: request.Question);

            var response = await _askKnowledgeHandler.HandleAsync(applicationRequest, cancellationToken);

            return Ok(response);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError("request", exception.Message);

            return ValidationProblem(ModelState);
        }
        catch (HttpRequestException)
        {
            return Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "AI service request failed.",
                detail: "The AI service could not be reached or returned an unsuccessful response.");
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return Problem(
                statusCode: StatusCodes.Status504GatewayTimeout,
                title: "AI service request timed out.",
                detail: "The AI service did not respond within the configured timeout.");
        }
    }
}

public sealed class AskKnowledgeApiRequest
{
    [Required]
    [StringLength(2000, MinimumLength = 1)]
    public string Question { get; init; } = string.Empty;
}
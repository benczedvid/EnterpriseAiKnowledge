using System.ComponentModel.DataAnnotations;
using EnterpriseAI.Application.Abstractions.AI;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseAI.Api.Controllers;

[ApiController]
[Route("api/dev/ai")]
public sealed class AiTestController : ControllerBase
{
    private readonly ILlmService _llmService;
    private readonly IWebHostEnvironment _environment;

    public AiTestController(ILlmService llmService, IWebHostEnvironment environment)
    {
        _llmService = llmService;
        _environment = environment;
    }

    [HttpPost("test")]
    public async Task<ActionResult<LlmResponse>> TestAsync(
        [FromBody] AiTestRequest request,
        CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            ModelState.AddModelError(nameof(request.Prompt), "Prompt must not be empty.");

            return ValidationProblem(ModelState);
        }

        var llmRequest = new LlmRequest(
            SystemPrompt:
                "Válaszolj magyarul, röviden és közérthetően. " +
                "Legfeljebb három mondatot írj.",
            UserPrompt: request.Prompt.Trim());

        try
        {
            var response = await _llmService.GenerateAsync(llmRequest, cancellationToken);

            return Ok(response);
        }
        catch (HttpRequestException)
        {
            return Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Ollama request failed.",
                detail:
                    "The language model service could not be reached " +
                    "or returned an unsuccessful HTTP response.");
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return Problem(
                statusCode: StatusCodes.Status504GatewayTimeout,
                title: "Ollama request timed out.",
                detail:
                    "The language model service did not respond " +
                    "within the configured timeout.");
        }
    }
}

public sealed class AiTestRequest
{
    [Required]
    [StringLength(2000, MinimumLength = 1)]
    public string Prompt { get; init; } = string.Empty;
}
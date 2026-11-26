using System.ComponentModel.DataAnnotations;
using EnterpriseAI.Application.Abstractions.AI;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseAI.Api.Controllers;

[ApiController]
[Route("api/dev/embeddings")]
public sealed class EmbeddingTestController : ControllerBase
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IWebHostEnvironment _environment;

    public EmbeddingTestController(IEmbeddingService embeddingService, IWebHostEnvironment environment)
    {
        _embeddingService = embeddingService;
        _environment = environment;
    }

    [HttpPost("test")]
    public async Task<ActionResult<EmbeddingTestResponse>> TestAsync(
        [FromBody] EmbeddingTestRequest request,
        CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        var embeddingRequest = new EmbeddingRequest(
            request.Inputs
                .Select(input => input.Trim())
                .ToArray());

        try
        {
            var result = await _embeddingService.CreateEmbeddingsAsync(embeddingRequest, cancellationToken);

            var response = new EmbeddingTestResponse(
                Count: result.Embeddings.Count,
                Dimensions: result.Dimensions,
                Samples: result.Embeddings
                    .Select(embedding => embedding.Take(5).ToArray())
                    .ToArray());

            return Ok(response);
        }
        catch (HttpRequestException)
        {
            return Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Embedding request failed.",
                detail: "The embedding service could not be reached or returned an unsuccessful HTTP response.");
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return Problem(
                statusCode: StatusCodes.Status504GatewayTimeout,
                title: "Embedding request timed out.",
                detail: "The embedding service did not respond within the configured timeout.");
        }
    }
}

public sealed class EmbeddingTestRequest
{
    [Required]
    [MinLength(1)]
    [MaxLength(16)]
    public string[] Inputs { get; init; } = [];
}

public sealed record EmbeddingTestResponse(
    int Count,
    int Dimensions,
    IReadOnlyList<IReadOnlyList<float>> Samples);
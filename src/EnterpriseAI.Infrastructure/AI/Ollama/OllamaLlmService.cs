using System.Net;
using System.Net.Http.Json;
using EnterpriseAI.Application.Abstractions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EnterpriseAI.Infrastructure.AI.Ollama;

public sealed class OllamaLlmService : ILlmService
{
    private readonly HttpClient _httpClient;
    private readonly OllamaOptions _options;
    private readonly ILogger<OllamaLlmService> _logger;
    private const int MaximumAttempts = 3;
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.Zero,
        TimeSpan.FromMilliseconds(750),
        TimeSpan.FromMilliseconds(1500)
    ];

    public OllamaLlmService(
        HttpClient httpClient,
        IOptions<OllamaOptions> options,
        ILogger<OllamaLlmService> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<LlmResponse> GenerateAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            throw new ArgumentException("System prompt must not be empty.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.UserPrompt))
        {
            throw new ArgumentException("User prompt must not be empty.", nameof(request));
        }

        var ollamaRequest = new OllamaChatRequest
        {
            Model = _options.Model,
            Think = _options.Think,
            Stream = false,
            KeepAlive = "30m",
            Options = new OllamaChatOptions
                {
                    NumberOfPredictedTokens = _options.MaximumOutputTokens,
                    Temperature = _options.Temperature
                },
            Messages =
            [
                new OllamaChatMessage
                {
                    Role = "system",
                    Content = request.SystemPrompt
                },
                new OllamaChatMessage
                {
                    Role = "user",
                    Content = request.UserPrompt
                }
            ]
        };

        using var response = await PostWithRetryAsync(
            ollamaRequest,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var ollamaResponse = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(cancellationToken: cancellationToken);

        if (ollamaResponse is null || !ollamaResponse.Done)
        {
            throw new InvalidOperationException("Ollama did not return a completed response.");
        }

        var content = ollamaResponse.Message?.Content;

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("Ollama returned an empty response.");
        }

        return new LlmResponse(content);
    }

    private async Task<HttpResponseMessage> PostWithRetryAsync(
        OllamaChatRequest ollamaRequest,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaximumAttempts; attempt++)
        {
            try
            {
                _logger.LogInformation(
                    "Sending Ollama request. Attempt {Attempt}/{MaximumAttempts}, model: {Model}.",
                    attempt,
                    MaximumAttempts,
                    ollamaRequest.Model);

                var response = await _httpClient.PostAsJsonAsync(
                    "api/chat",
                    ollamaRequest,
                    cancellationToken);

                if (!IsTransientStatusCode(response.StatusCode))
                {
                    _logger.LogInformation(
                        "Ollama request completed with HTTP {StatusCode} on attempt {Attempt}.",
                        (int)response.StatusCode,
                        attempt);

                    return response;
                }

                if (attempt == MaximumAttempts)
                {
                    _logger.LogError(
                        "Ollama request returned HTTP {StatusCode} after {AttemptCount} attempts.",
                        (int)response.StatusCode,
                        MaximumAttempts);

                    return response;
                }

                _logger.LogWarning(
                    "Ollama request returned transient HTTP {StatusCode} on attempt {Attempt}. Retrying.",
                    (int)response.StatusCode,
                    attempt);

                response.Dispose();
            }
            catch (HttpRequestException exception)
                when (attempt < MaximumAttempts)
            {
                _logger.LogWarning(
                    exception,
                    "Ollama request failed with an HTTP error on attempt {Attempt}. Retrying.",
                    attempt);
            }
            catch (TaskCanceledException exception)
                when (!cancellationToken.IsCancellationRequested &&
                    attempt < MaximumAttempts)
            {
                _logger.LogWarning(
                    exception,
                    "Ollama request timed out on attempt {Attempt}. Retrying.",
                    attempt);
            }

            var retryDelay = RetryDelays[attempt];

            _logger.LogInformation(
                "Waiting {DelayMilliseconds} ms before Ollama retry.",
                retryDelay.TotalMilliseconds);

            await Task.Delay(retryDelay, cancellationToken);
        }

        throw new InvalidOperationException(
            "Ollama request failed after all retry attempts.");
    }

    private static bool IsTransientStatusCode(HttpStatusCode statusCode)
    {
        return statusCode is
            HttpStatusCode.RequestTimeout or
            HttpStatusCode.TooManyRequests or
            HttpStatusCode.InternalServerError or
            HttpStatusCode.BadGateway or
            HttpStatusCode.ServiceUnavailable or
            HttpStatusCode.GatewayTimeout;
    }
}

using EnterpriseAI.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAI.Api.Controllers;

[ApiController]
[Route("api/dev/database")]
public sealed class DatabaseTestController : ControllerBase
{
    private readonly EnterpriseAiDbContext _dbContext;
    private readonly IWebHostEnvironment _environment;

    public DatabaseTestController(EnterpriseAiDbContext dbContext, IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _environment = environment;
    }

    [HttpGet("test")]
    public async Task<ActionResult<DatabaseTestResponse>> TestAsync(CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        try
        {
            var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);

            if (!canConnect)
            {
                return Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Database is unavailable.",
                    detail: "The application could not connect to the configured database.");
            }

            var vectorVersion = await GetVectorVersionAsync(cancellationToken);

            return Ok(new DatabaseTestResponse(
                CanConnect: true,
                Provider: _dbContext.Database.ProviderName ?? "Unknown",
                VectorExtensionVersion: vectorVersion));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Problem(
                statusCode: StatusCodes.Status504GatewayTimeout,
                title: "Database request timed out.",
                detail: "The database did not respond within the expected time.");
        }
        catch (Exception)
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Database test failed.",
                detail: "The database connection or pgvector verification failed.");
        }
    }

    private async Task<string?> GetVectorVersionAsync(CancellationToken cancellationToken)
    {
        await using var command = _dbContext.Database
            .GetDbConnection()
            .CreateCommand();

        command.CommandText = """
            SELECT extversion
            FROM pg_extension
            WHERE extname = 'vector';
            """;

        if (command.Connection?.State != System.Data.ConnectionState.Open)
        {
            await command.Connection!.OpenAsync(cancellationToken);
        }

        var result = await command.ExecuteScalarAsync(cancellationToken);

        return result?.ToString();
    }
}

public sealed record DatabaseTestResponse(
    bool CanConnect,
    string Provider,
    string? VectorExtensionVersion);
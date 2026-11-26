using EnterpriseAI.Infrastructure.Documents.SharePoint;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Graph;

namespace EnterpriseAI.Api.Controllers;

[ApiController]
[Route("api/dev/sharepoint")]
public sealed class SharePointTestController : ControllerBase
{
    private readonly GraphServiceClient _graphClient;
    private readonly SharePointOptions _options;
    private readonly IWebHostEnvironment _environment;

    public SharePointTestController(
        GraphServiceClient graphClient,
        IOptions<SharePointOptions> options,
        IWebHostEnvironment environment)
    {
        _graphClient = graphClient;
        _options = options.Value;
        _environment = environment;
    }

    [HttpGet("test")]
    public async Task<ActionResult<SharePointTestResponse>> TestAsync(CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        try
        {
            var site = await _graphClient
                .Sites[_options.SiteId]
                .GetAsync(
                    requestConfiguration =>
                    {
                        requestConfiguration.QueryParameters.Select =
                        [
                            "id",
                            "displayName",
                            "webUrl"
                        ];
                    },
                    cancellationToken);

            if (site is null)
            {
                return Problem(
                    statusCode: StatusCodes.Status502BadGateway,
                    title: "SharePoint site was not returned.",
                    detail: "Microsoft Graph returned an empty site response.");
            }

            var drive = await _graphClient
                .Drives[_options.DriveId]
                .GetAsync(
                    requestConfiguration =>
                    {
                        requestConfiguration.QueryParameters.Select =
                        [
                            "id",
                            "name",
                            "webUrl",
                            "driveType"
                        ];
                    },
                    cancellationToken);

            if (drive is null)
            {
                return Problem(
                    statusCode: StatusCodes.Status502BadGateway,
                    title: "SharePoint document library was not returned.",
                    detail: "Microsoft Graph returned an empty drive response.");
            }

            return Ok(
                new SharePointTestResponse(
                    Site: new SharePointSiteTestResponse(
                        Id: site.Id ?? string.Empty,
                        DisplayName: site.DisplayName ?? string.Empty,
                        WebUrl: site.WebUrl ?? string.Empty),
                    DocumentLibrary:
                        new SharePointDriveTestResponse(
                            Id: drive.Id ?? string.Empty,
                            Name: drive.Name ?? string.Empty,
                            WebUrl: drive.WebUrl ?? string.Empty,
                            DriveType: drive.DriveType ?? string.Empty)));
        }
        catch (Microsoft.Graph.Models.ODataErrors.ODataError exception)
        {
            return Problem(
                statusCode: MapGraphStatusCode(exception.ResponseStatusCode),
                title: "Microsoft Graph request failed.",
                detail: GetSafeGraphErrorMessage(exception));
        }
        catch (Azure.Identity.AuthenticationFailedException)
        {
            return Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Microsoft Graph authentication failed.",
                detail: "The configured Entra ID application could not authenticate with Microsoft Graph.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Problem(
                statusCode: StatusCodes.Status504GatewayTimeout,
                title: "Microsoft Graph request timed out.",
                detail: "Microsoft Graph did not respond within the expected time.");
        }
    }

    private static int MapGraphStatusCode(int statusCode)
    {
        return statusCode switch
        {
            StatusCodes.Status401Unauthorized => StatusCodes.Status502BadGateway,
            StatusCodes.Status403Forbidden => StatusCodes.Status403Forbidden,
            StatusCodes.Status404NotFound => StatusCodes.Status404NotFound,
            StatusCodes.Status429TooManyRequests => StatusCodes.Status503ServiceUnavailable,

            _ => StatusCodes.Status502BadGateway
        };
    }

    private static string GetSafeGraphErrorMessage(
        Microsoft.Graph.Models.ODataErrors.ODataError exception)
    {
        return exception.ResponseStatusCode switch
        {
            StatusCodes.Status403Forbidden =>
                "The application authenticated successfully but does not have read access to the configured SharePoint site.",

            StatusCodes.Status404NotFound =>
                "The configured SharePoint site or document library could not be found.",

            StatusCodes.Status429TooManyRequests =>
                "Microsoft Graph temporarily throttled the request.",

            _ =>
                "Microsoft Graph returned an unsuccessful response."
        };
    }
}

public sealed record SharePointTestResponse(
    SharePointSiteTestResponse Site,
    SharePointDriveTestResponse DocumentLibrary);

public sealed record SharePointSiteTestResponse(
    string Id,
    string DisplayName,
    string WebUrl);

public sealed record SharePointDriveTestResponse(
    string Id,
    string Name,
    string WebUrl,
    string DriveType);
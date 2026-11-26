namespace EnterpriseAI.Infrastructure.Documents.SharePoint;

public sealed class SharePointOptions
{
    public const string SectionName = "SharePoint";
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string SiteId { get; set; } = string.Empty;
    public string DriveId { get; set; } = string.Empty;
    public string? FolderPath { get; set; }
    public int MaximumFileSizeMegabytes { get; set; } = 25;
    public int DiscoveryPageSize { get; set; } = 200;
    public string ClientSecret { get; set; } = string.Empty;
}
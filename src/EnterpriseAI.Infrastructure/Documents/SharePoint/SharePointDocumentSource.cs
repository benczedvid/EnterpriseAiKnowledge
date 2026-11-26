using EnterpriseAI.Application.Abstractions.Documents;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Drives.Item.Items.Item.Children;
using Microsoft.Graph.Models;

namespace EnterpriseAI.Infrastructure.Documents.SharePoint;

public sealed class SharePointDocumentSource : IDocumentSource
{
    private const string SourceTypeName = "SharePoint";

    private readonly GraphServiceClient _graphClient;
    private readonly SharePointOptions _options;

    public SharePointDocumentSource(
        GraphServiceClient graphClient,
        IOptions<SharePointOptions> options)
    {
        _graphClient = graphClient;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<SourceDocument>> DiscoverAsync(DocumentSourceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.MaximumDocuments is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(query), "Maximum documents must be greater than zero.");
        }

        var allowedExtensions = NormalizeExtensions(query.AllowedExtensions);
        var maximumDocuments = query.MaximumDocuments ?? int.MaxValue;
        var startItem = await ResolveDiscoveryStartItemAsync(query.FolderPath, cancellationToken);
        var startItemId = GetRequiredValue(startItem.Id, "SharePoint discovery start item ID");
        var discoveredDocuments = new List<SourceDocument>();
        var pendingFolders = new Queue<string>();
        pendingFolders.Enqueue(startItemId);

        var visitedFolderIds = new HashSet<string>(StringComparer.Ordinal);

        while (pendingFolders.Count > 0 && discoveredDocuments.Count < maximumDocuments)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var folderId = pendingFolders.Dequeue();

            if (!visitedFolderIds.Add(folderId))
            {
                continue;
            }

            await DiscoverFolderAsync(
                folderId,
                allowedExtensions,
                maximumDocuments,
                discoveredDocuments,
                pendingFolders,
                cancellationToken);
        }

        return discoveredDocuments;
    }

    public async Task<Stream> DownloadAsync(string sourceId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            throw new ArgumentException("Source ID must not be empty.", nameof(sourceId));
        }

        var normalizedSourceId = sourceId.Trim();

        var item = await _graphClient
            .Drives[_options.DriveId]
            .Items[normalizedSourceId]
            .GetAsync(
                requestConfiguration =>
                {
                    requestConfiguration.QueryParameters.Select =
                    [
                        "id",
                        "name",
                        "size",
                        "file"
                    ];
                },
                cancellationToken);

        if (item is null)
        {
            throw new InvalidOperationException("The SharePoint item was not returned.");
        }

        if (item.File is null)
        {
            throw new InvalidOperationException("The SharePoint item is not a downloadable file.");
        }

        if (!IsAllowedSize(item.Size))
        {
            throw new InvalidOperationException("The SharePoint file is empty or exceeds the configured maximum file size.");
        }

        var content = await _graphClient
            .Drives[_options.DriveId]
            .Items[normalizedSourceId]
            .Content
            .GetAsync(cancellationToken: cancellationToken);

        if (content is null)
        {
            throw new InvalidOperationException("Microsoft Graph returned no file content.");
        }

        return content;
    }

    public async Task<SourceDocument?> GetByIdAsync(string sourceId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            throw new ArgumentException("Source ID must not be empty.", nameof(sourceId));
        }

        var normalizedSourceId = sourceId.Trim();

        var item = await _graphClient
            .Drives[_options.DriveId]
            .Items[normalizedSourceId]
            .GetAsync(
                requestConfiguration =>
                {
                    requestConfiguration.QueryParameters.Select =
                    [
                        "id",
                        "name",
                        "webUrl",
                        "eTag",
                        "size",
                        "createdDateTime",
                        "lastModifiedDateTime",
                        "file",
                        "folder",
                        "parentReference"
                    ];
                },
                cancellationToken);

        if (item is null)
        {
            return null;
        }

        if (item.File is null)
        {
            return null;
        }

        if (!IsAllowedSize(item.Size))
        {
            return null;
        }

        return MapDocument(item);
    }

    private SourceDocument MapDocument(DriveItem item)
    {
        var fileName = GetRequiredValue(item.Name, "SharePoint item name");

        return new SourceDocument(
            SourceId: GetRequiredValue(item.Id, "SharePoint item ID"),
            SourceType: SourceTypeName,
            SourceUrl: GetRequiredValue(item.WebUrl, "SharePoint item URL"),
            FileName: fileName,
            Title: Path.GetFileNameWithoutExtension(fileName),
            MimeType: item.File?.MimeType ?? "application/octet-stream",
            FolderPath: item.ParentReference?.Path,
            ETag: item.ETag,
            SizeInBytes: item.Size ?? 0,
            SourceCreatedAt: item.CreatedDateTime ?? DateTimeOffset.MinValue,
            SourceModifiedAt: item.LastModifiedDateTime ?? DateTimeOffset.MinValue);
    }

    private bool IsAllowedSize(long? size)
    {
        if (!size.HasValue)
        {
            return false;
        }

        var maximumBytes = (long)_options.MaximumFileSizeMegabytes * 1024 * 1024;

        return size.Value > 0 && size.Value <= maximumBytes;
    }

    private static HashSet<string> NormalizeExtensions(IReadOnlyCollection<string>? extensions)
    {
        if (extensions is null || extensions.Count == 0)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return extensions
            .Where(extension => !string.IsNullOrWhiteSpace(extension))
            .Select(extension =>
            {
                var normalized = extension.Trim();

                return normalized.StartsWith('.') ? normalized : $".{normalized}";
            })
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsAllowedExtension(string? fileName, IReadOnlySet<string> allowedExtensions)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        if (allowedExtensions.Count == 0)
        {
            return true;
        }

        var extension = Path.GetExtension(fileName);

        return allowedExtensions.Contains(extension);
    }

    private static string GetRequiredValue(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{fieldName} is missing.");
        }

        return value.Trim();
    }
    private async Task<DriveItem> ResolveDiscoveryStartItemAsync(string? folderPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || folderPath.Trim() == "/")
        {
            var rootItem = await _graphClient
                .Drives[_options.DriveId]
                .Root
                .GetAsync(
                    requestConfiguration =>
                    {
                        requestConfiguration.QueryParameters.Select =
                        [
                            "id",
                            "name",
                            "folder"
                        ];
                    },
                    cancellationToken);

            return rootItem ?? throw new InvalidOperationException("Microsoft Graph returned no drive root item.");
        }

        var normalizedFolderPath = folderPath
            .Trim()
            .Trim('/');

        if (string.IsNullOrWhiteSpace(normalizedFolderPath))
        {
            throw new ArgumentException("Folder path must not be empty.", nameof(folderPath));
        }

        var folderItem = await _graphClient
            .Drives[_options.DriveId]
            .Root
            .ItemWithPath(normalizedFolderPath)
            .GetAsync(
                requestConfiguration =>
                {
                    requestConfiguration.QueryParameters.Select =
                    [
                        "id",
                        "name",
                        "folder"
                    ];
                },
                cancellationToken);

        if (folderItem is null)
        {
            throw new InvalidOperationException("Microsoft Graph returned no item for the configured folder path.");
        }

        if (folderItem.Folder is null)
        {
            throw new InvalidOperationException("The configured SharePoint folder path does not reference a folder.");
        }

        return folderItem;
    }

    private async Task DiscoverFolderAsync(
        string folderId,
        IReadOnlySet<string> allowedExtensions,
        int maximumDocuments,
        ICollection<SourceDocument> discoveredDocuments,
        Queue<string> pendingFolders,
        CancellationToken cancellationToken)
    {
        var page = await GetChildrenPageAsync(folderId, cancellationToken);

        while (page is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var item in page.Value ?? [])
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (item.Folder is not null)
                {
                    var childFolderId = GetRequiredValue(item.Id, "SharePoint folder item ID");

                    pendingFolders.Enqueue(childFolderId);
                    continue;
                }

                if (item.File is null)
                {
                    continue;
                }

                if (!IsAllowedExtension(item.Name, allowedExtensions))
                {
                    continue;
                }

                if (!IsAllowedSize(item.Size))
                {
                    continue;
                }

                discoveredDocuments.Add(MapDocument(item));

                if (discoveredDocuments.Count >= maximumDocuments)
                {
                    return;
                }
            }

            if (string.IsNullOrWhiteSpace(page.OdataNextLink))
            {
                return;
            }

            page = await new ChildrenRequestBuilder(
                    page.OdataNextLink,
                    _graphClient.RequestAdapter)
                .GetAsync(cancellationToken: cancellationToken);
        }
    }

    private Task<DriveItemCollectionResponse?> GetChildrenPageAsync(string folderId, CancellationToken cancellationToken)
    {
        return _graphClient
            .Drives[_options.DriveId]
            .Items[folderId]
            .Children
            .GetAsync(
                requestConfiguration =>
                {
                    requestConfiguration.QueryParameters.Select =
                    [
                        "id",
                        "name",
                        "webUrl",
                        "eTag",
                        "size",
                        "createdDateTime",
                        "lastModifiedDateTime",
                        "file",
                        "folder",
                        "parentReference"
                    ];

                    requestConfiguration.QueryParameters.Top = _options.DiscoveryPageSize;
                },
                cancellationToken);
    }
}
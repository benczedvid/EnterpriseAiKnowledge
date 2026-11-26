namespace EnterpriseAI.Domain.Indexing;

public sealed class IndexingRun
{
    private const int MaximumFailureMessageLength = 2000;
    private readonly List<IndexingRunItem> _items = [];

    private IndexingRun()
    {
    }

    public IndexingRun(
        string sourceType,
        string? folderPath,
        int requestedMaximumDocuments,
        bool forceReindex)
    {
        if (string.IsNullOrWhiteSpace(sourceType))
        {
            throw new ArgumentException("Source type must not be empty.", nameof(sourceType));
        }

        if (requestedMaximumDocuments < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(requestedMaximumDocuments));
        }

        Id = Guid.NewGuid();
        SourceType = sourceType.Trim();
        FolderPath = NormalizeOptionalValue(folderPath);
        RequestedMaximumDocuments = requestedMaximumDocuments;
        ForceReindex = forceReindex;
        Status = IndexingRunStatus.Running;
        StartedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public string SourceType { get; private set; } = string.Empty;
    public string? FolderPath { get; private set; }
    public int RequestedMaximumDocuments { get; private set; }
    public bool ForceReindex { get; private set; }
    public IndexingRunStatus Status { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public int DiscoveredCount { get; private set; }
    public int ProcessedCount { get; private set; }
    public int IndexedCount { get; private set; }
    public int SkippedUnchangedCount { get; private set; }
    public int SkippedUnsupportedCount { get; private set; }
    public int RequiresOcrCount { get; private set; }
    public int FailedCount { get; private set; }
    public int NotFoundCount { get; private set; }
    public int TotalChunkCount { get; private set; }
    public string? FailureMessage { get; private set; }
    public IReadOnlyCollection<IndexingRunItem> Items => _items.AsReadOnly();
    public TimeSpan? Duration => CompletedAt.HasValue ? CompletedAt.Value - StartedAt : null;

    public void Complete(int discoveredCount)
    {
        EnsureRunning();

        ValidateCount(discoveredCount, nameof(discoveredCount));

        if (discoveredCount < ProcessedCount)
        {
            throw new InvalidOperationException("Discovered count must not be smaller than processed count.");
        }

        if (ProcessedCount != _items.Count)
        {
            throw new InvalidOperationException("Processed count must match the number of indexing run items.");
        }

        var statusTotal =
            IndexedCount
            + SkippedUnchangedCount
            + SkippedUnsupportedCount
            + RequiresOcrCount
            + FailedCount
            + NotFoundCount;

        if (ProcessedCount != statusTotal)
        {
            throw new InvalidOperationException("Processed count must match the sum of item result counts.");
        }

        var indexedChunkCount = _items
            .Where(item => item.Status == IndexingRunItemStatus.Indexed)
            .Sum(item => item.ChunkCount);

        if (TotalChunkCount != indexedChunkCount)
        {
            throw new InvalidOperationException("Total chunk count must match the chunks created by indexed run items.");
        }

        DiscoveredCount = discoveredCount;

        Status = FailedCount > 0
            ? IndexingRunStatus.CompletedWithErrors
            : IndexingRunStatus.Completed;

        FailureMessage = null;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void MarkAsFailed(string failureMessage)
    {
        EnsureRunning();

        FailureMessage = NormalizeFailureMessage(failureMessage);

        Status = IndexingRunStatus.Failed;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void MarkAsCancelled()
    {
        EnsureRunning();

        Status = IndexingRunStatus.Cancelled;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    private void EnsureRunning()
    {
        if (Status != IndexingRunStatus.Running)
        {
            throw new InvalidOperationException("Only a running indexing operation can be completed or terminated.");
        }
    }

    private static void ValidateCount(int value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Count must not be negative.");
        }
    }

    private static string NormalizeFailureMessage(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "An unspecified indexing error occurred.";
        }

        var normalized = value.Trim();

        return normalized.Length <= MaximumFailureMessageLength
            ? normalized
            : normalized[..MaximumFailureMessageLength];
    }

    private static string? NormalizeOptionalValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    public void AddItem(
        string sourceId,
        string? fileName,
        IndexingRunItemStatus status,
        string message,
        Guid? documentId,
        int chunkCount,
        int characterCount,
        DateTimeOffset? indexedAt)
    {
        EnsureRunning();

        var normalizedSourceId = sourceId?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedSourceId))
        {
            throw new ArgumentException("Source ID must not be empty.", nameof(sourceId));
        }

        if (_items.Any(item =>
                string.Equals(
                    item.SourceId,
                    normalizedSourceId,
                    StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"The indexing run already contains an item for source ID '{normalizedSourceId}'.");
        }

        var item = new IndexingRunItem(
            indexingRunId: Id,
            sourceId: normalizedSourceId,
            fileName: fileName,
            status: status,
            message: message,
            documentId: documentId,
            chunkCount: chunkCount,
            characterCount: characterCount,
            indexedAt: indexedAt);

        _items.Add(item);

        ProcessedCount++;

        switch (status)
        {
            case IndexingRunItemStatus.Indexed:
                IndexedCount++;
                TotalChunkCount += chunkCount;
                break;

            case IndexingRunItemStatus.SkippedUnchanged:
                SkippedUnchangedCount++;
                break;

            case IndexingRunItemStatus.SkippedUnsupported:
                SkippedUnsupportedCount++;
                break;

            case IndexingRunItemStatus.RequiresOcr:
                RequiresOcrCount++;
                break;

            case IndexingRunItemStatus.Failed:
                FailedCount++;
                break;

            case IndexingRunItemStatus.NotFound:
                NotFoundCount++;
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(status),
                    status,
                    "Unknown indexing run item status.");
        }
    }
}
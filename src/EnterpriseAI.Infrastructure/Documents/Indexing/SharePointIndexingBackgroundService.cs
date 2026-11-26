using EnterpriseAI.Application.Abstractions.Indexing;
using EnterpriseAI.Application.Documents.IndexSharePointBatch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EnterpriseAI.Infrastructure.Documents.Indexing;

public sealed class SharePointIndexingBackgroundService : BackgroundService
{
    private readonly ISharePointIndexingQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SharePointIndexingBackgroundService> _logger;

    public SharePointIndexingBackgroundService(
        ISharePointIndexingQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<SharePointIndexingBackgroundService> logger)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(logger);

        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SharePoint indexing background service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            SharePointIndexingJob job;

            try
            {
                job = await _queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                _logger.LogError(
                    exception,
                    "Failed to dequeue a SharePoint indexing job.");

                await DelayAfterFailureAsync(stoppingToken);

                continue;
            }

            await ProcessJobSafelyAsync(job, stoppingToken);
        }

        _logger.LogInformation("SharePoint indexing background service stopped.");
    }

    private async Task ProcessJobSafelyAsync(SharePointIndexingJob job, CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Starting SharePoint indexing run {IndexingRunId}. " +
            "Maximum documents: {MaximumDocuments}. " +
            "Force reindex: {ForceReindex}.",
            job.IndexingRunId,
            job.MaximumDocuments,
            job.ForceReindex);

        var startedAt = DateTimeOffset.UtcNow;

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();

            var handler = scope.ServiceProvider.GetRequiredService<ProcessSharePointIndexingJobHandler>();

            /*
             * A handler kizárólag a host stopping tokenjét kapja.
             * Az eredeti HTTP kérés tokenje itt már nem szerepel.
             */
            await handler.HandleAsync(job, stoppingToken);

            var duration = DateTimeOffset.UtcNow - startedAt;

            _logger.LogInformation(
                "SharePoint indexing run {IndexingRunId} completed in {DurationSeconds:F1} seconds.",
                job.IndexingRunId,
                duration.TotalSeconds);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "SharePoint indexing run {IndexingRunId} was cancelled because the application is stopping.",
                job.IndexingRunId);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            _logger.LogError(
                exception,
                "SharePoint indexing run {IndexingRunId} failed.",
                job.IndexingRunId);
        }
    }

    private async Task DelayAfterFailureAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Az alkalmazás leáll, nincs további teendő.
        }
    }
}
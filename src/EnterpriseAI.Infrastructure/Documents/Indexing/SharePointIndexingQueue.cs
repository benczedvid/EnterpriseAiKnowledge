using System.Threading.Channels;
using EnterpriseAI.Application.Abstractions.Indexing;

namespace EnterpriseAI.Infrastructure.Documents.Indexing;

public sealed class SharePointIndexingQueue : ISharePointIndexingQueue
{
    private const int QueueCapacity = 10;

    private readonly Channel<SharePointIndexingJob> _channel = Channel.CreateBounded<SharePointIndexingJob>(
            new BoundedChannelOptions(QueueCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            });

    public ValueTask EnqueueAsync(SharePointIndexingJob job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);

        return _channel.Writer.WriteAsync(job, cancellationToken);
    }

    public ValueTask<SharePointIndexingJob> DequeueAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAsync(cancellationToken);
    }
}
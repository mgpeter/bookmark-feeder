using System.Threading.Channels;

namespace BookmarkFeeder.WebApi.Services;

public interface IFaviconQueue
{
    /// <summary>
    /// Queues a bookmark for favicon discovery. Never blocks and never throws — it is called
    /// from the API request path. Returns false when the queue is full, in which case the
    /// bookmark keeps a null FaviconFetchedAt and the next startup backfill picks it up.
    /// </summary>
    bool TryEnqueue(Guid bookmarkId);

    IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken ct);
}

/// <summary>
/// In-memory work queue over a bounded channel. Bounded on purpose: work is cheap to
/// reconstruct (the backfill re-finds anything dropped), so a memory ceiling is worth more
/// than a guarantee of delivery.
/// </summary>
public class FaviconQueue(int capacity = 5000) : IFaviconQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateBounded<Guid>(
        new BoundedChannelOptions(capacity)
        {
            // Wait mode, but only ever written via TryWrite, which never blocks and returns
            // false when full. The Drop* modes would report success while silently discarding,
            // hiding a full queue from the caller.
            FullMode = BoundedChannelFullMode.Wait,
        });

    public bool TryEnqueue(Guid bookmarkId) => _channel.Writer.TryWrite(bookmarkId);

    public IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken ct) =>
        _channel.Reader.ReadAllAsync(ct);

    /// <summary>Closes the queue so consumers finish. For tests; the app's queue lives forever.</summary>
    public void Complete() => _channel.Writer.Complete();
}

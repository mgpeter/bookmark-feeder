using BookmarkFeeder.WebApi.Services;
using FluentAssertions;

namespace BookmarkFeeder.WebApi.Tests.Services;

public class FaviconQueueTests
{
    [Fact]
    public async Task Yields_EnqueuedIdsInOrder()
    {
        var queue = new FaviconQueue();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        queue.TryEnqueue(first).Should().BeTrue();
        queue.TryEnqueue(second).Should().BeTrue();
        queue.Complete();

        var read = new List<Guid>();
        await foreach (var id in queue.DequeueAllAsync(CancellationToken.None)) read.Add(id);

        read.Should().Equal(first, second);
    }

    [Fact]
    public void TryEnqueue_ReturnsFalse_WhenFull_RatherThanBlocking()
    {
        // Enqueue happens on the API request path, so a full queue must never block it.
        // A dropped id keeps FaviconFetchedAt null, so the next startup backfill retries it.
        var queue = new FaviconQueue(capacity: 2);

        queue.TryEnqueue(Guid.NewGuid()).Should().BeTrue();
        queue.TryEnqueue(Guid.NewGuid()).Should().BeTrue();
        queue.TryEnqueue(Guid.NewGuid()).Should().BeFalse();
    }
}

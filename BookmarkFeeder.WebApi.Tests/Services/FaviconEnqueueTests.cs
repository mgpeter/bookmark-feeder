using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using BookmarkFeeder.WebApi.Dtos;
using BookmarkFeeder.WebApi.Services;
using BookmarkFeeder.WebApi.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BookmarkFeeder.WebApi.Tests.Services;

/// <summary>
/// Bookmarks reach the favicon queue when they are created or synced. The queue is spied on,
/// so nothing is fetched.
/// </summary>
public class FaviconEnqueueTests(BookmarkApiFactory factory) : IClassFixture<BookmarkApiFactory>
{
    private sealed class SpyQueue : IFaviconQueue
    {
        public List<Guid> Enqueued { get; } = [];
        /// <summary>False simulates a full queue.</summary>
        public bool Accepts { get; set; } = true;

        public bool TryEnqueue(Guid bookmarkId)
        {
            if (Accepts) Enqueued.Add(bookmarkId);
            return Accepts;
        }

        public async IAsyncEnumerable<Guid> DequeueAllAsync(
            [EnumeratorCancellation] CancellationToken ct)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private (HttpClient Client, SpyQueue Queue) CreateClient()
    {
        var spy = new SpyQueue();
        var client = factory
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<IFaviconQueue>();
                services.AddSingleton<IFaviconQueue>(spy);
            }))
            .CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", BookmarkApiFactory.TestApiKey);
        return (client, spy);
    }

    [Fact]
    public async Task CreatingABookmark_QueuesItForFaviconDiscovery()
    {
        var (client, queue) = CreateClient();

        var response = await client.PostAsJsonAsync("/api/bookmarks", new CreateBookmarkRequest(
            $"https://enqueue-{Guid.NewGuid():N}.example.com", "Queued", null, null, null, null, null));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<BookmarkDto>();
        queue.Enqueued.Should().Contain(created!.Id);
    }

    [Fact]
    public async Task SyncingABatch_QueuesEveryBookmarkItCreated()
    {
        var (client, queue) = CreateClient();
        var token = Guid.NewGuid().ToString("N")[..8];

        var batch = new BatchCreateRequest([
            new BatchBookmarkItem($"https://{token}-1.example.com", "One", null, null, null),
            new BatchBookmarkItem($"https://{token}-2.example.com", "Two", null, null, null),
        ]);
        var result = await (await client.PostAsJsonAsync("/api/bookmarks/batch", batch))
            .Content.ReadFromJsonAsync<BatchResultDto>();

        result!.Summary.Created.Should().Be(2);
        queue.Enqueued.Should().HaveCount(2);
        queue.Enqueued.Should().BeEquivalentTo(result.Created.Select(c => c.Id));
    }

    [Fact]
    public async Task SyncingABatch_DoesNotQueueSkippedDuplicates()
    {
        var (client, queue) = CreateClient();
        var url = $"https://dupe-{Guid.NewGuid():N}.example.com";
        await client.PostAsJsonAsync("/api/bookmarks",
            new CreateBookmarkRequest(url, "First", null, null, null, null, null));
        queue.Enqueued.Clear();

        var batch = new BatchCreateRequest([new BatchBookmarkItem(url, "Again", null, null, null)]);
        var result = await (await client.PostAsJsonAsync("/api/bookmarks/batch", batch))
            .Content.ReadFromJsonAsync<BatchResultDto>();

        result!.Summary.Skipped.Should().Be(1);
        // Nothing was created, so there is nothing to fetch an icon for.
        queue.Enqueued.Should().BeEmpty();
    }

    [Fact]
    public async Task AFullQueue_DoesNotFailTheRequest()
    {
        var (client, queue) = CreateClient();
        queue.Accepts = false;

        // A dropped id keeps FaviconFetchedAt null, so the next startup backfill catches it.
        // Favicon enrichment must never be able to break creating a bookmark.
        var response = await client.PostAsJsonAsync("/api/bookmarks", new CreateBookmarkRequest(
            $"https://full-{Guid.NewGuid():N}.example.com", "Still created", null, null, null, null, null));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}

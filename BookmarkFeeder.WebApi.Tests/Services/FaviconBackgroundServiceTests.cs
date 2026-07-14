using BookmarkFeeder.WebApi.Data;
using BookmarkFeeder.WebApi.Models;
using BookmarkFeeder.WebApi.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BookmarkFeeder.WebApi.Tests.Services;

/// <summary>
/// The worker only loads a bookmark and saves it back, so InMemory is enough and no real
/// HTTP is involved — the resolver is faked.
/// </summary>
public class FaviconBackgroundServiceTests
{
    private sealed class FakeResolver : IFaviconResolver
    {
        public Func<string, string?> Behaviour { get; set; } = _ => null;
        public List<string> Seen { get; } = [];

        public Task<string?> ResolveAsync(string bookmarkUrl, CancellationToken ct = default)
        {
            lock (Seen) Seen.Add(bookmarkUrl);
            return Task.FromResult(Behaviour(bookmarkUrl));
        }
    }

    private sealed class Harness : IAsyncDisposable
    {
        public required ServiceProvider Provider { get; init; }
        public required FaviconQueue Queue { get; init; }
        public required FakeResolver Resolver { get; init; }
        public required FaviconBackgroundService Worker { get; init; }

        public IDbContextFactory<BookmarkDbContext> Db =>
            Provider.GetRequiredService<IDbContextFactory<BookmarkDbContext>>();

        public async ValueTask DisposeAsync()
        {
            await Worker.StopAsync(CancellationToken.None);
            await Provider.DisposeAsync();
        }
    }

    private static Harness Build()
    {
        var resolver = new FakeResolver();
        var queue = new FaviconQueue();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContextFactory<BookmarkDbContext>(o =>
            o.UseInMemoryDatabase($"favicon-{Guid.NewGuid()}"));
        services.AddSingleton<IFaviconResolver>(resolver);
        services.AddSingleton<IFaviconQueue>(queue);

        var provider = services.BuildServiceProvider();
        var worker = new FaviconBackgroundService(
            queue,
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<ILogger<FaviconBackgroundService>>());

        return new Harness { Provider = provider, Queue = queue, Resolver = resolver, Worker = worker };
    }

    private static async Task<Guid> AddBookmark(Harness harness, string url, bool isDeleted = false)
    {
        await using var db = await harness.Db.CreateDbContextAsync();
        var bookmark = new Bookmark
        {
            Id = Guid.NewGuid(),
            Url = url,
            Title = "Test",
            DateAdded = DateTime.UtcNow,
            DateModified = DateTime.UtcNow,
            IsDeleted = isDeleted,
        };
        db.Bookmarks.Add(bookmark);
        await db.SaveChangesAsync();
        return bookmark.Id;
    }

    private static async Task<Bookmark?> Reload(Harness harness, Guid id)
    {
        await using var db = await harness.Db.CreateDbContextAsync();
        return await db.Bookmarks.IgnoreQueryFilters().FirstOrDefaultAsync(b => b.Id == id);
    }

    private static async Task<bool> Eventually(Func<Task<bool>> check, int timeoutMs = 3000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (await check()) return true;
            await Task.Delay(25);
        }
        return false;
    }

    [Fact]
    public async Task Success_SetsTheFaviconUrlAndStampsTheAttempt()
    {
        await using var harness = Build();
        harness.Resolver.Behaviour = _ => "https://site.example/favicon.ico";
        var id = await AddBookmark(harness, "https://site.example/page");

        await harness.Worker.StartAsync(CancellationToken.None);
        harness.Queue.TryEnqueue(id);

        (await Eventually(async () => (await Reload(harness, id))?.FaviconUrl is not null))
            .Should().BeTrue("the worker should have resolved the favicon");

        var bookmark = await Reload(harness, id);
        bookmark!.FaviconUrl.Should().Be("https://site.example/favicon.ico");
        bookmark.FaviconFetchedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task NothingFound_StampsTheAttempt_ButLeavesTheUrlNull()
    {
        await using var harness = Build();
        harness.Resolver.Behaviour = _ => null;
        var id = await AddBookmark(harness, "https://no-icon.example/page");

        await harness.Worker.StartAsync(CancellationToken.None);
        harness.Queue.TryEnqueue(id);

        // The stamp is what stops the backfill retrying this site on every startup.
        (await Eventually(async () => (await Reload(harness, id))?.FaviconFetchedAt is not null))
            .Should().BeTrue("a failed attempt must still be recorded");

        (await Reload(harness, id))!.FaviconUrl.Should().BeNull();
    }

    [Fact]
    public async Task AThrowingResolver_DoesNotKillTheWorker()
    {
        await using var harness = Build();
        var poison = await AddBookmark(harness, "https://boom.example/page");
        var healthy = await AddBookmark(harness, "https://fine.example/page");
        harness.Resolver.Behaviour = url =>
            url.Contains("boom") ? throw new InvalidOperationException("kaboom") : "https://fine.example/i.png";

        await harness.Worker.StartAsync(CancellationToken.None);
        harness.Queue.TryEnqueue(poison);
        harness.Queue.TryEnqueue(healthy);

        // The second id proves the loop survived the first.
        (await Eventually(async () => (await Reload(harness, healthy))?.FaviconUrl is not null))
            .Should().BeTrue("one bad site must not stop the queue");

        // The poison bookmark is still stamped, so it isn't retried forever.
        (await Reload(harness, poison))!.FaviconFetchedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task SoftDeletedBookmarks_AreSkipped()
    {
        await using var harness = Build();
        harness.Resolver.Behaviour = _ => "https://site.example/favicon.ico";
        var id = await AddBookmark(harness, "https://deleted.example/page", isDeleted: true);

        await harness.Worker.StartAsync(CancellationToken.None);
        harness.Queue.TryEnqueue(id);
        await Task.Delay(300);

        harness.Resolver.Seen.Should().BeEmpty("a deleted bookmark should never be fetched");
        (await Reload(harness, id))!.FaviconUrl.Should().BeNull();
    }

    [Fact]
    public async Task Backfill_PicksUpBookmarksNeverAttempted()
    {
        await using var harness = Build();
        harness.Resolver.Behaviour = _ => "https://site.example/favicon.ico";
        var never = await AddBookmark(harness, "https://never-tried.example/page");

        // Nothing is enqueued by the test: starting the worker is what should find it.
        await harness.Worker.StartAsync(CancellationToken.None);

        (await Eventually(async () => (await Reload(harness, never))?.FaviconUrl is not null))
            .Should().BeTrue("startup backfill should queue bookmarks with no attempt recorded");
    }

    [Fact]
    public async Task Backfill_SkipsBookmarksAlreadyAttempted()
    {
        await using var harness = Build();
        harness.Resolver.Behaviour = _ => "https://site.example/favicon.ico";
        var attempted = await AddBookmark(harness, "https://no-icon-here.example/page");

        // Mark it as already tried and found nothing — the state a failed attempt leaves behind.
        await using (var db = await harness.Db.CreateDbContextAsync())
        {
            var bookmark = await db.Bookmarks.FirstAsync(b => b.Id == attempted);
            bookmark.FaviconFetchedAt = DateTime.UtcNow.AddDays(-1);
            await db.SaveChangesAsync();
        }

        await harness.Worker.StartAsync(CancellationToken.None);
        await Task.Delay(300);

        // Re-fetching it on every startup is exactly what the stamp exists to prevent.
        harness.Resolver.Seen.Should().BeEmpty();
        (await Reload(harness, attempted))!.FaviconUrl.Should().BeNull();
    }

    [Fact]
    public async Task AnUnknownId_IsIgnored()
    {
        await using var harness = Build();
        harness.Resolver.Behaviour = _ => "https://site.example/favicon.ico";

        await harness.Worker.StartAsync(CancellationToken.None);
        harness.Queue.TryEnqueue(Guid.NewGuid());
        await Task.Delay(300);

        harness.Resolver.Seen.Should().BeEmpty();
    }
}

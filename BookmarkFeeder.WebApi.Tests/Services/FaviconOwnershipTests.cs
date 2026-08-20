using BookmarkFeeder.WebApi.Auth;
using BookmarkFeeder.WebApi.Data;
using BookmarkFeeder.WebApi.Models;
using BookmarkFeeder.WebApi.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BookmarkFeeder.WebApi.Tests.Services;

/// <summary>
/// The favicon worker runs on its own scope with no HTTP request, so there is no current user and
/// ICurrentUserContext.UserId is Guid.Empty - which matches no rows.
///
/// This fixture reproduces that exactly (note: NO AddTestCurrentUser). Without
/// IgnoreQueryFilters(["Owner"]) in the worker, the backfill returns nothing and favicon
/// enrichment dies silently for every account, with no other test noticing.
/// </summary>
public class FaviconOwnershipTests
{
    private static readonly Guid Alice = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid Bob = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    private sealed class StubResolver : IFaviconResolver
    {
        public Task<string?> ResolveAsync(string url, CancellationToken ct = default) =>
            Task.FromResult<string?>($"{url.TrimEnd('/')}/favicon.ico");
    }

    private static ServiceProvider BuildUnscopedProvider(string databaseName)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        // Deliberately NO ICurrentUserContext registration of a resolved user: a scoped, unresolved
        // context is what the worker actually gets in production.
        services.AddScoped<ICurrentUserContext, CurrentUserContext>();
        services.AddDbContextFactory<BookmarkDbContext>(
            o => o.UseInMemoryDatabase(databaseName), ServiceLifetime.Scoped);
        services.AddSingleton<IFaviconResolver>(new StubResolver());
        services.AddSingleton<IFaviconQueue>(new FaviconQueue());

        return services.BuildServiceProvider();
    }

    private static async Task SeedTwoOwnersAsync(ServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<BookmarkDbContext>>();
        await using var db = await factory.CreateDbContextAsync();

        foreach (var (owner, url) in new[] { (Alice, "https://alice.test"), (Bob, "https://bob.test") })
        {
            db.Bookmarks.Add(new Bookmark
            {
                Id = Guid.NewGuid(),
                OwnerId = owner,
                Url = url,
                Title = "Needs an icon",
                DateAdded = DateTime.UtcNow,
                DateModified = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task TheBackfill_SeesBookmarksFromEveryOwner()
    {
        var provider = BuildUnscopedProvider($"favicon-owners-{Guid.NewGuid()}");
        await SeedTwoOwnersAsync(provider);

        using var scope = provider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<BookmarkDbContext>>();
        await using var db = await factory.CreateDbContextAsync();

        // The worker's own query, verbatim. Without the bypass this is zero and enrichment stops.
        var pending = await db.Bookmarks
            .IgnoreQueryFilters(["Owner"])
            .Where(b => b.FaviconFetchedAt == null)
            .Select(b => b.Id)
            .ToListAsync();

        pending.Should().HaveCount(2, "the worker is cross-owner by design");
    }

    [Fact]
    public async Task WithoutTheBypass_TheWorkerWouldSeeNothing()
    {
        // Pins down WHY the bypass is needed, so removing it fails a test that explains itself
        // rather than one that just goes quiet.
        var provider = BuildUnscopedProvider($"favicon-noowner-{Guid.NewGuid()}");
        await SeedTwoOwnersAsync(provider);

        using var scope = provider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<BookmarkDbContext>>();
        await using var db = await factory.CreateDbContextAsync();

        var withFilter = await db.Bookmarks.Where(b => b.FaviconFetchedAt == null).ToListAsync();

        withFilter.Should().BeEmpty(
            "an unresolved owner is Guid.Empty, which matches nothing - fail closed");
    }

    [Fact]
    public async Task TheWorker_EnrichesBookmarksAcrossOwners()
    {
        var provider = BuildUnscopedProvider($"favicon-e2e-{Guid.NewGuid()}");
        await SeedTwoOwnersAsync(provider);

        var worker = new FaviconBackgroundService(
            provider.GetRequiredService<IFaviconQueue>(),
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<ILogger<FaviconBackgroundService>>());

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await worker.StartAsync(cts.Token);

        // The queue drains asynchronously; poll rather than guess at a delay.
        using var scope = provider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<BookmarkDbContext>>();

        List<Bookmark> enriched = [];
        while (!cts.IsCancellationRequested)
        {
            await using var db = await factory.CreateDbContextAsync(cts.Token);
            enriched = await db.Bookmarks
                .IgnoreQueryFilters(["Owner"])
                .Where(b => b.FaviconFetchedAt != null)
                .ToListAsync(cts.Token);

            if (enriched.Count == 2) break;
            await Task.Delay(50, cts.Token);
        }

        await worker.StopAsync(CancellationToken.None);

        enriched.Should().HaveCount(2);
        enriched.Select(b => b.OwnerId).Should().BeEquivalentTo([Alice, Bob]);
        enriched.Should().OnlyContain(b => b.FaviconUrl != null);
    }
}

using System.Net;
using System.Net.Http.Json;
using BookmarkFeeder.WebApi.Dtos;
using FluentAssertions;

namespace BookmarkFeeder.WebApi.Tests.Infrastructure;

/// <summary>
/// Isolation behaviours that EF InMemory cannot model: ExecuteUpdate sweeps, the per-owner unique
/// indexes, and full-text search. Real PostgreSQL, because these are exactly the paths where a
/// missing owner predicate would not show up anywhere else.
/// </summary>
[Trait("Category", "Integration")]
public class UserIsolationPostgresTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private static string Token() => "iso" + Guid.NewGuid().ToString("N")[..10];

    private HttpClient Alice => factory.CreateAuthenticatedClient("auth0|pg-alice");
    private HttpClient Bob => factory.CreateAuthenticatedClient("auth0|pg-bob");

    [Fact]
    public async Task MarkAllRead_DoesNotReachAnotherAccount()
    {
        var token = Token();
        await Alice.PostAsJsonAsync("/api/bookmarks",
            new { url = $"https://{token}.test/", title = $"Alice {token}" });

        // No filters: the widest possible sweep, which is why it must stay owner-scoped. This runs
        // through ExecuteUpdateAsync, where a missing predicate would silently cross accounts.
        var response = await Bob.PostAsJsonAsync("/api/bookmarks/mark-read", new { isRead = true });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var alicesUnread = await Alice.GetStringAsync($"/api/bookmarks?q={token}&isRead=false");
        alicesUnread.Should().Contain(token, "Bob's sweep must not have touched Alice's bookmark");
    }

    [Fact]
    public async Task TwoAccounts_CanSaveTheSameUrl()
    {
        // The single most visible consequence of making the Url index per-owner. Under the old
        // global unique index this second insert was a 409.
        var url = $"https://{Token()}.shared.test/";

        var alice = await Alice.PostAsJsonAsync("/api/bookmarks", new { url, title = "Alice's copy" });
        var bob = await Bob.PostAsJsonAsync("/api/bookmarks", new { url, title = "Bob's copy" });

        alice.StatusCode.Should().Be(HttpStatusCode.Created);
        bob.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task TheSameAccount_StillCannotSaveAUrlTwice()
    {
        var url = $"https://{Token()}.dupe.test/";

        (await Alice.PostAsJsonAsync("/api/bookmarks", new { url, title = "First" }))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await Alice.PostAsJsonAsync("/api/bookmarks", new { url, title = "Second" }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task TwoAccounts_CanHoldTagsOfTheSameName()
    {
        var name = Token();

        (await Alice.PostAsJsonAsync("/api/tags", new { name })).StatusCode
            .Should().Be(HttpStatusCode.Created);
        (await Bob.PostAsJsonAsync("/api/tags", new { name })).StatusCode
            .Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task ABatchSync_DoesNotSkipUrlsAnotherAccountHappensToHave()
    {
        // The regression guard for BookmarkService's batch dedupe. With a bare IgnoreQueryFilters()
        // there, a new user's very first sync would report everything as "already present" purely
        // because somebody else had saved it - and would silently import nothing.
        var token = Token();
        var urls = Enumerable.Range(0, 3).Select(i => $"https://{token}-{i}.test/").ToArray();

        var alicesBatch = await Alice.PostAsJsonAsync("/api/bookmarks/batch", new
        {
            bookmarks = urls.Select(u => new { url = u, title = u }).ToArray(),
            skipDuplicates = true,
        });
        var alicesResult = await alicesBatch.Content.ReadFromJsonAsync<BatchResultDto>();
        alicesResult!.Created.Should().HaveCount(3);

        var bobsBatch = await Bob.PostAsJsonAsync("/api/bookmarks/batch", new
        {
            bookmarks = urls.Select(u => new { url = u, title = u }).ToArray(),
            skipDuplicates = true,
        });
        var bobsResult = await bobsBatch.Content.ReadFromJsonAsync<BatchResultDto>();

        bobsResult!.Created.Should().HaveCount(3, "the same URLs are new to Bob");
        bobsResult.Skipped.Should().BeEmpty();
    }

    [Fact]
    public async Task FullTextSearch_DoesNotLeakAcrossAccounts()
    {
        // Search builds two independent root queries and UNIONs their ids. If ownership were
        // applied per-query rather than as a global filter, this is the branch that would be missed.
        var token = Token();
        await Alice.PostAsJsonAsync("/api/bookmarks",
            new { url = $"https://{token}.test/", title = $"Alice {token} secret" });

        var bobsResults = await Bob.GetStringAsync($"/api/bookmarks?q={token}");

        bobsResults.Should().NotContain(token);
    }
}

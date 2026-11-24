using System.Net;
using System.Net.Http.Json;
using BookmarkFeeder.WebApi.Dtos;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BookmarkFeeder.WebApi.Tests.Infrastructure;

/// <summary>
/// Two accounts, one database. Nothing one creates may be visible or reachable from the other.
///
/// Note the 404s: a request for another account's row must not be answered with 403, which would
/// confirm the id exists.
/// </summary>
public class UserIsolationTests(BookmarkApiFactory factory) : IClassFixture<BookmarkApiFactory>
{
    private HttpClient Alice => factory.CreateAuthenticatedClient("auth0|iso-alice");
    private HttpClient Bob => factory.CreateAuthenticatedClient("auth0|iso-bob");

    private static async Task<Guid> CreateBookmarkAsync(HttpClient client, string url, string title)
    {
        var response = await client.PostAsJsonAsync("/api/bookmarks", new { url, title });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        return Guid.Parse(dto!["id"].ToString()!);
    }

    [Fact]
    public async Task OneAccountsBookmarks_AreInvisibleToAnother()
    {
        await CreateBookmarkAsync(Alice, "https://iso-list.test/", "Alice only");

        var bobsList = await Bob.GetStringAsync("/api/bookmarks?pageSize=100");

        bobsList.Should().NotContain("iso-list.test");
    }

    [Fact]
    public async Task FetchingAnotherAccountsBookmark_Is404()
    {
        var id = await CreateBookmarkAsync(Alice, "https://iso-fetch.test/", "Alice only");

        var response = await Bob.GetAsync($"/api/bookmarks/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task MutatingAnotherAccountsBookmark_Is404(string method)
    {
        var id = await CreateBookmarkAsync(Alice, $"https://iso-mutate-{method}.test/", "Alice only");

        var request = new HttpRequestMessage(new HttpMethod(method), $"/api/bookmarks/{id}");
        if (method == "PUT")
        {
            request.Content = JsonContent.Create(new { title = "Hijacked" });
        }

        var response = await Bob.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MarkingAnotherAccountsBookmarkRead_Is404()
    {
        var id = await CreateBookmarkAsync(Alice, "https://iso-read.test/", "Alice only");

        var response = await Bob.PatchAsJsonAsync($"/api/bookmarks/{id}/read", new { isRead = true });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TagsAreScopedPerAccount()
    {
        await Alice.PostAsJsonAsync("/api/tags", new { name = "alice-private-tag" });

        var bobsTags = await Bob.GetStringAsync("/api/tags");

        bobsTags.Should().NotContain("alice-private-tag");
    }

    [Fact]
    public async Task CategoriesAreScopedPerAccount()
    {
        await Alice.PostAsJsonAsync("/api/categories", new { name = "alice-private-category" });

        var bobsCategories = await Bob.GetStringAsync("/api/categories");

        bobsCategories.Should().NotContain("alice-private-category");
    }

    [Fact]
    public async Task SavedSearchesAreScopedPerAccount()
    {
        await Alice.PostAsJsonAsync("/api/searches", new { name = "alice-private-search", query = "q=x" });

        var bobsSearches = await Bob.GetStringAsync("/api/searches");

        bobsSearches.Should().NotContain("alice-private-search");
    }

    [Fact]
    public async Task AnotherAccountsCategory_CannotBeAssignedToABookmark()
    {
        var created = await Alice.PostAsJsonAsync("/api/categories", new { name = "alice-cat-target" });
        var category = await created.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        var categoryId = Guid.Parse(category!["id"].ToString()!);

        var response = await Bob.PostAsJsonAsync("/api/bookmarks", new
        {
            url = "https://iso-category.test/",
            title = "Bob's",
            categoryId,
        });

        // Rejected as an invalid category rather than a permission error - Bob is told nothing
        // about whether that id exists.
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ABearerTokenCarryingTheKeyOwnerClaim_DoesNotActAsThatOwner()
    {
        // Only the API-key handler may assert bmf:owner. A JWT that carried it (an Auth0 Action
        // could add one) must resolve to its own subject, not to the named account.
        var victimClient = factory.CreateAuthenticatedClient(BookmarkApiFactory.OtherSub);
        var secret = $"https://victim-{Guid.NewGuid():N}.example.com";
        (await victimClient.PostAsJsonAsync("/api/bookmarks",
            new CreateBookmarkRequest(secret, "Victim's", null, null, null, null, null)))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        await using var db = factory.CreateContext();
        var victimId = (await db.Users.SingleAsync(u => u.Auth0Sub == BookmarkApiFactory.OtherSub)).Id;

        var attacker = factory.CreateClient();
        attacker.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer",
            TestTokens.Create("auth0|attacker", extraClaims: new Dictionary<string, object>
            {
                [BookmarkFeeder.WebApi.Auth.ApiKeyAuthenticationHandler.OwnerIdClaim] = victimId.ToString(),
            }));

        (await attacker.GetStringAsync("/api/bookmarks?pageSize=100")).Should().NotContain(secret);
    }
}

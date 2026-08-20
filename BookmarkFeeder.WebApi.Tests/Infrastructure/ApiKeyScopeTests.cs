using System.Net;
using System.Net.Http.Json;
using BookmarkFeeder.WebApi.Dtos;
using FluentAssertions;

namespace BookmarkFeeder.WebApi.Tests.Infrastructure;

/// <summary>
/// The fail-closed guarantee: a minted API key reaches an explicit allow-list and nothing else.
///
/// These are the tests that keep the guarantee true as the API grows - an endpoint added later
/// without [AllowApiKey] is denied automatically, and <see cref="EveryOtherEndpoint_RejectsAKey"/>
/// proves it rather than trusting anyone to remember.
/// </summary>
public class ApiKeyScopeTests(BookmarkApiFactory factory) : IClassFixture<BookmarkApiFactory>
{
    private async Task<HttpClient> KeyClientAsync(string sub = BookmarkApiFactory.DefaultSub)
    {
        // The account must exist before it can mint anything: keys are issued to users, and users
        // are provisioned just-in-time on their first authenticated request.
        var owner = factory.CreateAuthenticatedClient(sub);
        var created = await owner.PostAsJsonAsync("/api/keys", new CreateApiKeyRequest("Test key", null));
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var dto = await created.Content.ReadFromJsonAsync<CreatedApiKeyDto>();
        return factory.CreateKeyClient(dto!.Key);
    }

    [Fact]
    public async Task AKey_CanBatchCreateBookmarks()
    {
        var client = await KeyClientAsync();

        var response = await client.PostAsJsonAsync("/api/bookmarks/batch", new
        {
            bookmarks = new[] { new { url = "https://scope-batch.test/", title = "Batch" } },
            skipDuplicates = true,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AKey_CanReadTagsAndCategories()
    {
        var client = await KeyClientAsync();

        // GET /tags is what the extension's "Test connection" button calls.
        (await client.GetAsync("/api/tags")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/categories")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Everything the extension does not need. A key leaked from a browser profile must not be
    /// able to destroy a collection, and 403 (not 401) says the credential was understood and
    /// deliberately refused.
    /// </summary>
    [Theory]
    [InlineData("GET", "/api/bookmarks")]
    [InlineData("GET", "/api/searches")]
    [InlineData("GET", "/api/keys")]
    [InlineData("POST", "/api/bookmarks")]
    [InlineData("POST", "/api/bookmarks/mark-read")]
    [InlineData("POST", "/api/searches")]
    [InlineData("POST", "/api/keys")]
    [InlineData("POST", "/api/tags")]
    [InlineData("POST", "/api/categories")]
    [InlineData("DELETE", "/api/bookmarks/11111111-1111-1111-1111-111111111111")]
    [InlineData("DELETE", "/api/tags/11111111-1111-1111-1111-111111111111")]
    [InlineData("DELETE", "/api/categories/11111111-1111-1111-1111-111111111111")]
    [InlineData("DELETE", "/api/searches/11111111-1111-1111-1111-111111111111")]
    [InlineData("DELETE", "/api/keys/11111111-1111-1111-1111-111111111111")]
    [InlineData("PUT", "/api/bookmarks/11111111-1111-1111-1111-111111111111")]
    [InlineData("PATCH", "/api/bookmarks/11111111-1111-1111-1111-111111111111/read")]
    public async Task EveryOtherEndpoint_RejectsAKey(string method, string path)
    {
        var client = await KeyClientAsync();

        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method is "POST" or "PUT" or "PATCH")
        {
            request.Content = JsonContent.Create(new { });
        }

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(
            HttpStatusCode.Forbidden,
            "a sync-scoped key must be refused before the endpoint runs, so {0} {1} can never be " +
            "reached with one", method, path);
    }

    [Fact]
    public async Task AKey_CannotMintMoreKeys()
    {
        // Called out separately because it is the privilege-escalation case: if a key could mint
        // keys, revoking one would not contain a compromise.
        var client = await KeyClientAsync();

        var response = await client.PostAsJsonAsync(
            "/api/keys", new CreateApiKeyRequest("Escalated", null));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

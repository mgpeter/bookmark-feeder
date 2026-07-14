using System.Net;
using System.Net.Http.Json;
using BookmarkFeeder.WebApi.Common;
using BookmarkFeeder.WebApi.Dtos;
using FluentAssertions;

namespace BookmarkFeeder.WebApi.Tests.Infrastructure;

/// <summary>
/// Ranked full-text search on GET /api/bookmarks. Postgres-only by nature (tsvector,
/// websearch_to_tsquery, ts_rank), so it lives in the Testcontainers suite.
/// Each test uses a unique token so it only matches its own rows.
/// </summary>
[Trait("Category", "Integration")]
public class SearchQueryTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private static string Token() => "z" + Guid.NewGuid().ToString("N")[..10];

    private async Task<BookmarkDto> Create(
        HttpClient client, string url, string title, string? description = null, string[]? tags = null)
    {
        var response = await client.PostAsJsonAsync("/api/bookmarks",
            new CreateBookmarkRequest(url, title, description, null, null, null, tags));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<BookmarkDto>())!;
    }

    private async Task<PagedResult<BookmarkDto>> Search(HttpClient client, string queryString) =>
        (await factory.CreateAuthenticatedClient()
            .GetFromJsonAsync<PagedResult<BookmarkDto>>($"/api/bookmarks?{queryString}"))!;

    [Fact]
    public async Task Search_MatchesTitle_AndStemsTheTerm()
    {
        var client = factory.CreateAuthenticatedClient();
        var token = Token();
        await Create(client, $"https://{token}.example.com", $"{token} performance tuning");

        // Stemming is the point of FTS over ILIKE: "tuned" and "tuning" share a stem,
        // so this matches even though the literal substring never appears.
        var page = await Search(client, $"search={token}+tuned");

        page.Data.Should().ContainSingle().Which.Title.Should().Contain(token);
    }

    [Fact]
    public async Task Search_IsCaseInsensitive()
    {
        var client = factory.CreateAuthenticatedClient();
        var token = Token();
        await Create(client, $"https://{token}.example.com", $"Guide about {token.ToUpperInvariant()}");

        var page = await Search(client, $"search={token.ToLowerInvariant()}");

        page.Data.Should().ContainSingle();
    }

    [Fact]
    public async Task Search_MatchesDescription()
    {
        var client = factory.CreateAuthenticatedClient();
        var token = Token();
        await Create(client, "https://desc-" + token + ".example.com", "Untitled", $"notes mentioning {token}");

        var page = await Search(client, $"search={token}");

        page.Data.Should().ContainSingle();
    }

    [Fact]
    public async Task Search_MatchesWordInsideUrl()
    {
        var client = factory.CreateAuthenticatedClient();
        var token = Token();
        // The token exists only in the host; Postgres would otherwise index the whole host
        // as one lexeme and this would find nothing.
        await Create(client, $"https://{token}.netlify.app/guide", "Untitled page");

        var page = await Search(client, $"search={token}");

        page.Data.Should().ContainSingle();
    }

    [Fact]
    public async Task Search_MatchesTagName()
    {
        var client = factory.CreateAuthenticatedClient();
        var token = Token();
        // Neither the url nor the title carries the token, so a match can only come from the
        // tag — which lives outside the tsvector and is matched separately.
        await Create(client, $"https://plain-{Guid.NewGuid():N}.example.com", "Nothing relevant in here",
            null, [token]);

        var page = await Search(client, $"search={token}");

        page.Data.Should().ContainSingle().Which.Tags.Should().Contain(t => t.Name == token);
    }

    [Fact]
    public async Task Search_RanksTitleMatchesAboveUrlOnlyMatches()
    {
        var client = factory.CreateAuthenticatedClient();
        var token = Token();
        // The title match is created FIRST, so it is the OLDER row: under the previous
        // dateAdded-desc default it would come last. Only ranking can lift it to the top.
        await Create(client, "https://other.example.com/thing", $"All about {token}");
        await Create(client, $"https://{token}.example.com/page", "No mention in this title");

        var page = await Search(client, $"search={token}");

        page.Data.Should().HaveCount(2);
        // Weight A (title) must outrank weight C (url).
        page.Data[0].Title.Should().Contain(token);
    }

    [Fact]
    public async Task Search_DefaultsToRelevance_ButRespectsAnExplicitSort()
    {
        var client = factory.CreateAuthenticatedClient();
        var token = Token();
        // Alpha (the denser match) is created first, so date order would rank it LAST.
        await Create(client, $"https://{token}-a.example.com/{token}/{token}", $"Alpha {token} {token} {token}");
        await Create(client, $"https://{token}-b.example.com", $"Beta {token}");

        // Explicit sort wins over the relevance default.
        var byTitle = await Search(client, $"search={token}&sortBy=title&sortOrder=asc");
        byTitle.Data.Select(b => b.Title[..5]).Should().Equal("Alpha", "Beta ");

        // Without sortBy, the denser match comes first rather than the newest.
        var byRelevance = await Search(client, $"search={token}");
        byRelevance.Data[0].Title.Should().StartWith("Alpha");
    }

    [Fact]
    public async Task Search_SupportsWebsearchSyntax_QuotesAndNegation()
    {
        var client = factory.CreateAuthenticatedClient();
        var token = Token();
        await Create(client, $"https://{token}-1.example.com", $"{token} alpha beta");
        await Create(client, $"https://{token}-2.example.com", $"{token} alpha gamma");

        var negated = await Search(client, $"search={token}+alpha+-beta");
        negated.Data.Should().ContainSingle().Which.Title.Should().Contain("gamma");

        var phrase = await Search(client, $"search=%22{token}+alpha%22");
        phrase.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task Search_ComposesWithFiltersAndPagination()
    {
        var client = factory.CreateAuthenticatedClient();
        var token = Token();
        var read = await Create(client, $"https://{token}-r.example.com", $"{token} first");
        await Create(client, $"https://{token}-u1.example.com", $"{token} second");
        await Create(client, $"https://{token}-u2.example.com", $"{token} third");

        (await client.PatchAsJsonAsync($"/api/bookmarks/{read.Id}/read", new MarkReadRequest(true)))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var unread = await Search(client, $"search={token}&isRead=false");
        unread.Pagination.TotalItems.Should().Be(2);

        var paged = await Search(client, $"search={token}&pageSize=2");
        paged.Data.Should().HaveCount(2);
        paged.Pagination.TotalItems.Should().Be(3);
        paged.Pagination.TotalPages.Should().Be(2);
    }

    [Fact]
    public async Task Search_WithNoMatches_ReturnsEmptyPage()
    {
        var page = await Search(factory.CreateAuthenticatedClient(), $"search={Token()}");

        page.Data.Should().BeEmpty();
        page.Pagination.TotalItems.Should().Be(0);
    }

    [Theory]
    // websearch_to_tsquery is designed to swallow junk rather than throw, but the endpoint
    // takes raw user input — a 500 here would be a live bug.
    [InlineData("%22unclosed")]
    [InlineData("%26%7C%21")]
    [InlineData("-")]
    [InlineData("%3C%3E%3D")]
    [InlineData("a+OR+OR+b")]
    public async Task Search_WithMalformedQuery_DoesNotError(string raw)
    {
        var response = await factory.CreateAuthenticatedClient().GetAsync($"/api/bookmarks?search={raw}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SortByRelevance_WithoutSearchTerm_FallsBackInsteadOfFailing()
    {
        // There is no rank to sort by without a query; must not throw.
        var response = await factory.CreateAuthenticatedClient()
            .GetAsync("/api/bookmarks?sortBy=relevance&pageSize=5");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Search_TiedRanks_AreOrderedDeterministically()
    {
        var client = factory.CreateAuthenticatedClient();
        var token = Token();
        // Identical shape => identical rank. Without a tiebreaker Postgres may return tied
        // rows in any order, which makes paging skip or repeat rows between requests.
        foreach (var n in new[] { "one", "two", "three", "four" })
        {
            await Create(client, $"https://{token}-{n}.example.com", $"{token} entry");
        }

        var first = await Search(client, $"search={token}&pageSize=4");
        var second = await Search(client, $"search={token}&pageSize=4");

        first.Data.Select(b => b.Id).Should().Equal(second.Data.Select(b => b.Id));
        // Newest first, matching the list's default expectation.
        first.Data.Select(b => b.DateAdded).Should().BeInDescendingOrder();
    }
}

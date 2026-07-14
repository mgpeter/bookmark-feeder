using System.Net;
using System.Net.Http.Json;
using BookmarkFeeder.WebApi.Dtos;
using FluentAssertions;

namespace BookmarkFeeder.WebApi.Tests.Infrastructure;

/// <summary>
/// Saved searches: store a query + filter string and re-run it later. Single-tenant, no user
/// scoping. Real PostgreSQL so the table and its constraints are exercised.
/// </summary>
[Trait("Category", "Integration")]
public class SavedSearchTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private static string Name() => "s" + Guid.NewGuid().ToString("N")[..10];

    private async Task<SavedSearchDto> Create(HttpClient client, string name, string query)
    {
        var response = await client.PostAsJsonAsync("/api/searches", new CreateSavedSearchRequest(name, query));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<SavedSearchDto>())!;
    }

    [Fact]
    public async Task SavedSearch_RoundTrips_CreateListDelete()
    {
        var client = factory.CreateAuthenticatedClient();
        var name = Name();
        // The query is stored opaquely — it is whatever param string the UI was using.
        const string query = "q=graphql&tags=dotnet&sortBy=relevance";

        var created = await Create(client, name, query);
        created.Name.Should().Be(name);
        created.Query.Should().Be(query);
        created.Id.Should().NotBeEmpty();
        created.DateCreated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));

        var all = await client.GetFromJsonAsync<List<SavedSearchDto>>("/api/searches");
        all.Should().Contain(s => s.Id == created.Id && s.Query == query);

        (await client.DeleteAsync($"/api/searches/{created.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterDelete = await client.GetFromJsonAsync<List<SavedSearchDto>>("/api/searches");
        afterDelete.Should().NotContain(s => s.Id == created.Id);
    }

    [Fact]
    public async Task CreateSavedSearch_ReturnsALocationForTheNewResource()
    {
        var client = factory.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/searches",
            new CreateSavedSearchRequest(Name(), "q=x"));

        var dto = await response.Content.ReadFromJsonAsync<SavedSearchDto>();
        response.Headers.Location!.ToString().Should().EndWith($"/api/searches/{dto!.Id}");
    }

    [Theory]
    [InlineData("", "q=x")]
    [InlineData("   ", "q=x")]
    [InlineData("valid", "")]
    [InlineData("valid", "   ")]
    public async Task CreateSavedSearch_RejectsEmptyNameOrQuery(string name, string query)
    {
        var response = await factory.CreateAuthenticatedClient()
            .PostAsJsonAsync("/api/searches", new CreateSavedSearchRequest(name, query));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateSavedSearch_RejectsAnOverlongQuery()
    {
        // Query is varchar(2048); a longer one must fail validation rather than the database.
        var response = await factory.CreateAuthenticatedClient()
            .PostAsJsonAsync("/api/searches", new CreateSavedSearchRequest(Name(), new string('x', 2049)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteSavedSearch_IsNotFound_ForAnUnknownId()
    {
        var response = await factory.CreateAuthenticatedClient()
            .DeleteAsync($"/api/searches/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SavedSearches_RequireTheApiKey()
    {
        var client = factory.CreateClient();

        (await client.GetAsync("/api/searches")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync("/api/searches", new CreateSavedSearchRequest("x", "q=x")))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.DeleteAsync($"/api/searches/{Guid.NewGuid()}"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SavedSearches_AreListedNewestFirst()
    {
        var client = factory.CreateAuthenticatedClient();
        var first = await Create(client, Name(), "q=first");
        await Task.Delay(10);
        var second = await Create(client, Name(), "q=second");

        var all = await client.GetFromJsonAsync<List<SavedSearchDto>>("/api/searches");

        var ids = all!.Select(s => s.Id).ToList();
        ids.IndexOf(second.Id).Should().BeLessThan(ids.IndexOf(first.Id));
    }
}

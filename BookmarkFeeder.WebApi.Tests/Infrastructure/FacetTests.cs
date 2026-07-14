using System.Net;
using System.Net.Http.Json;
using BookmarkFeeder.WebApi.Dtos;
using FluentAssertions;

namespace BookmarkFeeder.WebApi.Tests.Infrastructure;

/// <summary>
/// Facet counts on the list response. Real PostgreSQL because facets are computed over the
/// same filtered query as the results, including the tsvector search filter.
/// </summary>
[Trait("Category", "Integration")]
public class FacetTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private static string Token() => "f" + Guid.NewGuid().ToString("N")[..10];

    private async Task<BookmarkDto> Create(
        HttpClient client, string url, string title, Guid? categoryId = null, string[]? tags = null)
    {
        var response = await client.PostAsJsonAsync("/api/bookmarks",
            new CreateBookmarkRequest(url, title, null, null, null, categoryId, tags));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<BookmarkDto>())!;
    }

    private async Task<Guid> CreateCategory(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/categories",
            new CreateCategoryRequest(name, null, null));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CategoryDto>())!.Id;
    }

    private async Task<BookmarkListResult> List(HttpClient client, string filters) =>
        (await client.GetFromJsonAsync<BookmarkListResult>($"/api/bookmarks?{filters}"))!;

    [Fact]
    public async Task Facets_AreAbsent_WhenNothingIsNarrowingTheSet()
    {
        var client = factory.CreateAuthenticatedClient();

        // Computing facets over the whole collection on every plain list load would be wasted
        // work — there is nothing to refine.
        var page = await List(client, "pageSize=5");

        page.Facets.Should().BeNull();
    }

    [Fact]
    public async Task Facets_CountTagsAcrossTheWholeMatch_NotJustThePage()
    {
        var client = factory.CreateAuthenticatedClient();
        var token = Token();
        var tag = $"{token}tag";
        foreach (var n in new[] { "a", "b", "c" })
        {
            await Create(client, $"https://{token}-{n}.example.com", $"{token} {n}", null, [tag]);
        }

        // One result per page, but the facet must describe all three matches.
        var page = await List(client, $"search={token}&pageSize=1");

        page.Data.Should().HaveCount(1);
        page.Facets!.Tags.Should().ContainSingle(f => f.Name == tag)
            .Which.Count.Should().Be(3);
    }

    [Fact]
    public async Task Facets_ReflectTheSearchFilter()
    {
        var client = factory.CreateAuthenticatedClient();
        var token = Token();
        // The tag names must not contain the search token: search also matches tag names, so a
        // tag like "<token>shared" would pull its bookmark into the result set on its own.
        var shared = Token();
        var only = Token();
        await Create(client, $"https://{token}-1.example.com", $"{token} first", null, [shared, only]);
        await Create(client, $"https://{token}-2.example.com", $"{token} second", null, [shared]);
        // Outside the search, so it must not contribute to the counts. Its url must not carry the
        // token either — url words are indexed, so it would match through the url alone.
        await Create(client, $"https://{Guid.NewGuid():N}.example.com", "unrelated title", null, [shared]);

        var page = await List(client, $"search={token}");

        page.Facets!.Tags.Should().Contain(f => f.Name == shared && f.Count == 2);
        page.Facets.Tags.Should().Contain(f => f.Name == only && f.Count == 1);
    }

    [Fact]
    public async Task Facets_CountCategories()
    {
        var client = factory.CreateAuthenticatedClient();
        var token = Token();
        var categoryName = $"{token}cat";
        var categoryId = await CreateCategory(client, categoryName);
        await Create(client, $"https://{token}-c1.example.com", $"{token} one", categoryId);
        await Create(client, $"https://{token}-c2.example.com", $"{token} two", categoryId);
        // Uncategorised: must not appear as a null-named bucket.
        await Create(client, $"https://{token}-c3.example.com", $"{token} three");

        var page = await List(client, $"search={token}");

        page.Facets!.Categories.Should().ContainSingle()
            .Which.Should().Match<FacetItemDto>(f => f.Name == categoryName && f.Count == 2);
    }

    [Fact]
    public async Task Facets_ArePresent_WhenOnlyAFilterIsActive_WithoutASearch()
    {
        var client = factory.CreateAuthenticatedClient();
        var token = Token();
        var tag = $"{token}tag";
        await Create(client, $"https://{token}-nf.example.com", $"{token} filtered", null, [tag]);

        // A tag filter narrows the set just as a search does.
        var page = await List(client, $"tags={tag}");

        page.Facets.Should().NotBeNull();
        page.Facets!.Tags.Should().Contain(f => f.Name == tag && f.Count == 1);
    }

    [Fact]
    public async Task Facets_AreEmpty_WhenTheSearchMatchesNothing()
    {
        var page = await List(factory.CreateAuthenticatedClient(), $"search={Token()}");

        page.Facets!.Tags.Should().BeEmpty();
        page.Facets.Categories.Should().BeEmpty();
    }

    [Fact]
    public async Task Facets_DoNotBreakExistingConsumers_DataAndPaginationUnchanged()
    {
        var client = factory.CreateAuthenticatedClient();
        var token = Token();
        await Create(client, $"https://{token}-compat.example.com", $"{token} compat");

        // The addition is additive: the old shape still deserialises.
        var legacy = await client.GetFromJsonAsync<BookmarkFeeder.WebApi.Common.PagedResult<BookmarkDto>>(
            $"/api/bookmarks?search={token}");

        legacy!.Data.Should().ContainSingle();
        legacy.Pagination.TotalItems.Should().Be(1);
    }
}

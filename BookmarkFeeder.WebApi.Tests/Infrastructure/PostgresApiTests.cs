using System.Net;
using System.Net.Http.Json;
using BookmarkFeeder.WebApi.Common;
using BookmarkFeeder.WebApi.Dtos;
using FluentAssertions;

namespace BookmarkFeeder.WebApi.Tests.Infrastructure;

// Real-PostgreSQL tests (Testcontainers). Filter out with:  dotnet test --filter Category!=Integration
[Trait("Category", "Integration")]
public class PostgresApiTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private async Task<BookmarkDto> CreateBookmark(HttpClient client, CreateBookmarkRequest request)
    {
        var response = await client.PostAsJsonAsync("/api/bookmarks", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<BookmarkDto>())!;
    }

    [Fact]
    public async Task Search_IsCaseInsensitive_ViaILike()
    {
        var client = factory.CreateAuthenticatedClient();
        var token = Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
        var url = $"https://s-{token}.example.com";

        await CreateBookmark(client, new CreateBookmarkRequest(url, $"Guide about {token}", null, null, null, null, null));

        // Title holds an UPPERCASE token; searching lowercase must still match (ILIKE, Postgres-only).
        var page = await client.GetFromJsonAsync<PagedResult<BookmarkDto>>(
            $"/api/bookmarks?search={token.ToLowerInvariant()}");

        page!.Data.Should().ContainSingle(b => b.Url == url);
    }

    [Fact]
    public async Task Batch_SkipsDuplicate_EvenWhenExistingRowIsSoftDeleted()
    {
        var client = factory.CreateAuthenticatedClient();
        var url = $"https://dup-{Guid.NewGuid():N}.example.com";

        var created = await CreateBookmark(client, new CreateBookmarkRequest(url, "Dup", null, null, null, null, null));
        (await client.DeleteAsync($"/api/bookmarks/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Re-sending the same URL must be SKIPPED, not re-inserted — the unique index still covers
        // the soft-deleted row, so a re-insert would throw. This only surfaces on real Postgres.
        var batch = new BatchCreateRequest(
            [new BatchBookmarkItem(url, "Dup again", null, null, null)], SkipDuplicates: true);
        var result = await (await client.PostAsJsonAsync("/api/bookmarks/batch", batch))
            .Content.ReadFromJsonAsync<BatchResultDto>();

        result!.Summary.Created.Should().Be(0);
        result.Summary.Skipped.Should().Be(1);
    }

    [Fact]
    public async Task DeletingTag_CascadesJoinRows_ButKeepsBookmark()
    {
        var client = factory.CreateAuthenticatedClient();
        var tagName = $"t{Guid.NewGuid():N}";
        var url = $"https://tag-{Guid.NewGuid():N}.example.com";

        var bookmark = await CreateBookmark(client,
            new CreateBookmarkRequest(url, "Tagged", null, null, null, null, [tagName]));
        bookmark.Tags.Should().ContainSingle(t => t.Name == tagName);

        var tags = await client.GetFromJsonAsync<List<TagDto>>("/api/tags");
        var tagId = tags!.Single(t => t.Name == tagName).Id;
        (await client.DeleteAsync($"/api/tags/{tagId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = await client.GetFromJsonAsync<BookmarkDto>($"/api/bookmarks/{bookmark.Id}");
        after!.Tags.Should().BeEmpty();
    }

    [Fact]
    public async Task DeletingCategory_SetsBookmarkCategoryToNull()
    {
        var client = factory.CreateAuthenticatedClient();
        var catName = $"c{Guid.NewGuid():N}";
        var category = await (await client.PostAsJsonAsync("/api/categories",
            new CreateCategoryRequest(catName, null, null))).Content.ReadFromJsonAsync<CategoryDto>();

        var url = $"https://cat-{Guid.NewGuid():N}.example.com";
        var bookmark = await CreateBookmark(client,
            new CreateBookmarkRequest(url, "InCat", null, null, null, category!.Id, null));
        bookmark.Categories.Should().ContainSingle(c => c.Id == category.Id);

        // Delete with no reassign target → FK SetNull detaches the bookmark's category.
        (await client.DeleteAsync($"/api/categories/{category.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = await client.GetFromJsonAsync<BookmarkDto>($"/api/bookmarks/{bookmark.Id}");
        after!.Categories.Should().BeEmpty();
    }
}

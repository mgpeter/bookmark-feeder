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

    // Search behaviour now lives in SearchQueryTests (tsvector-backed); case-insensitivity is
    // still pinned there by Search_IsCaseInsensitive.

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
    public async Task ConcurrentOverlappingBatches_AllSucceed_AndStoreEachUrlOnce()
    {
        // Two browsers syncing the same folders at once. Whoever loses the race at the unique
        // index used to get a 500 and lose its whole batch.
        var client = factory.CreateAuthenticatedClient();
        var run = Guid.NewGuid().ToString("N");
        var items = Enumerable.Range(0, 40)
            .Select(i => new BatchBookmarkItem($"https://race-{run}-{i}.example.com", $"R{i}", null, null, null))
            .ToList();
        var batch = new BatchCreateRequest(items, DefaultTags: [$"race{run}"], SkipDuplicates: true);

        var responses = await Task.WhenAll(Enumerable.Range(0, 4)
            .Select(_ => client.PostAsJsonAsync("/api/bookmarks/batch", batch)));

        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);
        var results = await Task.WhenAll(responses.Select(r => r.Content.ReadFromJsonAsync<BatchResultDto>()));
        results.Sum(r => r!.Summary.Created).Should().Be(40, "each URL is stored exactly once");
        results.Should().OnlyContain(r => r!.Summary.Created + r.Summary.Skipped == 40);
    }

    [Fact]
    public async Task ConcurrentCreates_IntroducingTheSameNewTag_AllSucceed()
    {
        var client = factory.CreateAuthenticatedClient();
        var tag = $"new{Guid.NewGuid():N}";

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(i =>
            client.PostAsJsonAsync("/api/bookmarks", new CreateBookmarkRequest(
                $"https://tagrace-{Guid.NewGuid():N}-{i}.example.com", "T", null, null, null, null, [tag]))));

        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.Created);
        var tags = await client.GetFromJsonAsync<List<TagDto>>("/api/tags");
        tags!.Where(t => t.Name == tag).Should().ContainSingle().Which.BookmarkCount.Should().Be(4);
    }

    [Fact]
    public async Task Batch_TreatsUrlsDifferingOnlyInCase_AsDifferentBookmarks()
    {
        var client = factory.CreateAuthenticatedClient();
        var id = Guid.NewGuid().ToString("N")[..8];
        var batch = new BatchCreateRequest(
        [
            new BatchBookmarkItem($"https://video.example.com/watch?v=ab{id}D", "Upper", null, null, null),
            new BatchBookmarkItem($"https://video.example.com/watch?v=ab{id}d", "Lower", null, null, null),
        ], SkipDuplicates: true);

        var result = await (await client.PostAsJsonAsync("/api/bookmarks/batch", batch))
            .Content.ReadFromJsonAsync<BatchResultDto>();

        result!.Summary.Created.Should().Be(2);
    }

    [Theory]
    [InlineData(1_700_000_000_000_000L)] // microseconds, not milliseconds
    [InlineData(-62_200_000_000_000L)]
    [InlineData(long.MaxValue)]
    public async Task Batch_WithAnImpossibleDateAdded_StillStoresTheBookmark(long dateAdded)
    {
        var client = factory.CreateAuthenticatedClient();
        var url = $"https://date-{Guid.NewGuid():N}.example.com";
        var batch = new BatchCreateRequest(
            [new BatchBookmarkItem(url, "Odd date", null, null, dateAdded)], SkipDuplicates: true);

        var response = await client.PostAsJsonAsync("/api/bookmarks/batch", batch);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<BatchResultDto>())!.Summary.Created.Should().Be(1);
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

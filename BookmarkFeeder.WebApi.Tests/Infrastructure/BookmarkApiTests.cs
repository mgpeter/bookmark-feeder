using System.Net;
using System.Net.Http.Json;
using BookmarkFeeder.WebApi.Common;
using BookmarkFeeder.WebApi.Dtos;
using FluentAssertions;

namespace BookmarkFeeder.WebApi.Tests.Infrastructure;

public class BookmarkApiTests(BookmarkApiFactory factory) : IClassFixture<BookmarkApiFactory>
{
    [Fact]
    public async Task Request_WithoutApiKey_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/bookmarks");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Request_WithApiKey_Succeeds()
    {
        var client = factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/bookmarks");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Batch_CreatesBookmarks_MapsEpochToUtc_AndSkipsDuplicates()
    {
        var client = factory.CreateAuthenticatedClient();
        const long epochMs = 1_699_999_999_000; // 2023-11-14T22:13:19Z
        var url = $"https://batch-{Guid.NewGuid()}.example.com";

        var request = new BatchCreateRequest(
            [new BatchBookmarkItem(url, "Batch Item", null, "Bar/Tech", epochMs)],
            DefaultTags: ["imported"],
            SkipDuplicates: true);

        var first = await client.PostAsJsonAsync("/api/bookmarks/batch", request);
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstResult = await first.Content.ReadFromJsonAsync<BatchResultDto>();
        firstResult!.Summary.Created.Should().Be(1);
        firstResult.Summary.Skipped.Should().Be(0);

        // Re-sending the same URL is skipped as a duplicate.
        var second = await client.PostAsJsonAsync("/api/bookmarks/batch", request);
        var secondResult = await second.Content.ReadFromJsonAsync<BatchResultDto>();
        secondResult!.Summary.Created.Should().Be(0);
        secondResult.Summary.Skipped.Should().Be(1);

        // Epoch ms mapped to a UTC DateAdded.
        var created = firstResult.Created.Single();
        var fetched = await client.GetFromJsonAsync<BookmarkDto>($"/api/bookmarks/{created.Id}");
        fetched!.DateAdded.Should().Be(DateTime.UnixEpoch.AddMilliseconds(epochMs));
        fetched.SourceFolder.Should().Be("Bar/Tech");
        fetched.Tags.Should().ContainSingle(t => t.NormalizedName == "imported");
    }

    [Fact]
    public async Task Post_DuplicateUrl_Returns409()
    {
        var client = factory.CreateAuthenticatedClient();
        var url = $"https://dup-{Guid.NewGuid()}.example.com";
        var request = new CreateBookmarkRequest(url, "Dup", null, null, null, null, null);

        (await client.PostAsJsonAsync("/api/bookmarks", request)).StatusCode.Should().Be(HttpStatusCode.Created);
        (await client.PostAsJsonAsync("/api/bookmarks", request)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Get_Paginates_AndFiltersByReadState()
    {
        var client = factory.CreateAuthenticatedClient();
        var tag = Guid.NewGuid().ToString("N");

        // Seed three bookmarks tagged uniquely so this test is isolated from others.
        var batch = new BatchCreateRequest(
            [
                new BatchBookmarkItem($"https://p-{tag}-1.example.com", "One", null, null, null),
                new BatchBookmarkItem($"https://p-{tag}-2.example.com", "Two", null, null, null),
                new BatchBookmarkItem($"https://p-{tag}-3.example.com", "Three", null, null, null),
            ],
            DefaultTags: [tag]);
        await client.PostAsJsonAsync("/api/bookmarks/batch", batch);

        var page = await client.GetFromJsonAsync<PagedResult<BookmarkDto>>(
            $"/api/bookmarks?tags={tag}&pageSize=2&page=1");
        page!.Data.Should().HaveCount(2);
        page.Pagination.TotalItems.Should().Be(3);
        page.Pagination.TotalPages.Should().Be(2);

        // Mark one read, then filter by isRead=true.
        var target = page.Data.First();
        var patch = await client.PatchAsJsonAsync($"/api/bookmarks/{target.Id}/read", new MarkReadRequest(true));
        patch.StatusCode.Should().Be(HttpStatusCode.OK);

        var readOnly = await client.GetFromJsonAsync<PagedResult<BookmarkDto>>(
            $"/api/bookmarks?tags={tag}&isRead=true");
        readOnly!.Data.Should().ContainSingle(b => b.Id == target.Id);
    }

    [Fact]
    public async Task Delete_SoftDeletes_AndHidesFromList()
    {
        var client = factory.CreateAuthenticatedClient();
        var tag = Guid.NewGuid().ToString("N");
        await client.PostAsJsonAsync("/api/bookmarks/batch", new BatchCreateRequest(
            [new BatchBookmarkItem($"https://del-{tag}.example.com", "Del", null, null, null)],
            DefaultTags: [tag]));

        var listed = await client.GetFromJsonAsync<PagedResult<BookmarkDto>>($"/api/bookmarks?tags={tag}");
        var id = listed!.Data.Single().Id;

        var delete = await client.DeleteAsync($"/api/bookmarks/{id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await client.GetAsync($"/api/bookmarks/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var afterDelete = await client.GetFromJsonAsync<PagedResult<BookmarkDto>>($"/api/bookmarks?tags={tag}");
        afterDelete!.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task Categories_CanBeCreated_AndReturnedAsTree()
    {
        var client = factory.CreateAuthenticatedClient();

        var parent = await (await client.PostAsJsonAsync("/api/categories",
            new CreateCategoryRequest($"Parent-{Guid.NewGuid():N}", null, null)))
            .Content.ReadFromJsonAsync<CategoryDto>();

        var child = await client.PostAsJsonAsync("/api/categories",
            new CreateCategoryRequest($"Child-{Guid.NewGuid():N}", null, parent!.Id));
        child.StatusCode.Should().Be(HttpStatusCode.Created);

        var tree = await client.GetFromJsonAsync<List<CategoryDto>>("/api/categories");
        var parentNode = tree!.Single(c => c.Id == parent.Id);
        parentNode.Children.Should().ContainSingle();
        parentNode.Children.Single().Level.Should().Be(1);
    }
}

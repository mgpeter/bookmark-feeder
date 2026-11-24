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

    [Fact]
    public async Task Update_CanClearTheCategoryAndDescription()
    {
        var client = factory.CreateAuthenticatedClient();
        var category = await (await client.PostAsJsonAsync("/api/categories",
            new CreateCategoryRequest($"Clear-{Guid.NewGuid():N}", null, null))).Content.ReadFromJsonAsync<CategoryDto>();
        var created = await (await client.PostAsJsonAsync("/api/bookmarks", new CreateBookmarkRequest(
            $"https://clear-{Guid.NewGuid():N}.example.com", "Clear me", "Some notes", null, null, category!.Id, null)))
            .Content.ReadFromJsonAsync<BookmarkDto>();

        var response = await client.PutAsJsonAsync($"/api/bookmarks/{created!.Id}",
            new UpdateBookmarkRequest("Clear me", "", null, null, null, null, ClearCategory: true));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<BookmarkDto>();
        updated!.Description.Should().BeNull();
        updated.Categories.Should().BeEmpty();
    }

    [Fact]
    public async Task Update_WithNullFields_LeavesThemUnchanged()
    {
        var client = factory.CreateAuthenticatedClient();
        var created = await (await client.PostAsJsonAsync("/api/bookmarks", new CreateBookmarkRequest(
            $"https://keep-{Guid.NewGuid():N}.example.com", "Keep", "Keep these notes", null, null, null, null)))
            .Content.ReadFromJsonAsync<BookmarkDto>();

        var updated = await (await client.PutAsJsonAsync($"/api/bookmarks/{created!.Id}",
            new UpdateBookmarkRequest("Renamed", null, null, null, null, null)))
            .Content.ReadFromJsonAsync<BookmarkDto>();

        updated!.Description.Should().Be("Keep these notes");
    }

    [Fact]
    public async Task Update_WithBothCategoryAndClearCategory_IsRejected()
    {
        var client = factory.CreateAuthenticatedClient();

        var response = await client.PutAsJsonAsync($"/api/bookmarks/{Guid.NewGuid()}",
            new UpdateBookmarkRequest(null, null, null, null, Guid.NewGuid(), null, ClearCategory: true));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static IEnumerable<CategoryDto> Flatten(IEnumerable<CategoryDto> nodes) =>
        nodes.SelectMany(n => new[] { n }.Concat(Flatten(n.Children)));

    [Theory]
    [InlineData("child")]
    [InlineData("grandchild")]
    public async Task DeletingACategory_ReassignedToItsOwnDescendant_KeepsTheTreeIntact(string target)
    {
        // A > B > C. Reassigning A's contents to B or C used to re-parent B onto the target:
        // B became its own parent, or B and C each other's, and the branch vanished from the tree.
        using var isolated = new BookmarkApiFactory();
        var client = isolated.CreateAuthenticatedClient();
        async Task<CategoryDto> Create(string name, Guid? parent) =>
            (await (await client.PostAsJsonAsync("/api/categories", new CreateCategoryRequest(name, null, parent)))
                .Content.ReadFromJsonAsync<CategoryDto>())!;
        var a = await Create("A", null);
        var b = await Create("B", a.Id);
        var c = await Create("C", b.Id);
        var targetId = target == "child" ? b.Id : c.Id;

        (await client.DeleteAsync($"/api/categories/{a.Id}?reassignTo={targetId}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var tree = Flatten((await client.GetFromJsonAsync<List<CategoryDto>>("/api/categories"))!).ToList();
        tree.Select(n => n.Name).Should().BeEquivalentTo(["B", "C"], "nothing may drop out of the tree");
        tree.Single(n => n.Id == targetId).ParentCategoryId.Should().BeNull("the target takes A's place");
    }

    [Fact]
    public async Task Batch_OverTheSizeCap_IsRejected()
    {
        var client = factory.CreateAuthenticatedClient();
        var items = Enumerable.Range(0, BookmarkFeeder.WebApi.Validators.BatchCreateRequestValidator.MaxBatchItems + 1)
            .Select(i => new BatchBookmarkItem($"https://cap-{i}.example.com", "x", null, null, null))
            .ToList();

        var response = await client.PostAsJsonAsync("/api/bookmarks/batch", new BatchCreateRequest(items));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("at most");
    }
}

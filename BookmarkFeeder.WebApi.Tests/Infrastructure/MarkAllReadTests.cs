using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BookmarkFeeder.WebApi.Common;
using BookmarkFeeder.WebApi.Dtos;
using FluentAssertions;

namespace BookmarkFeeder.WebApi.Tests.Infrastructure;

/// <summary>
/// Bulk mark-read. Real PostgreSQL because the whole point is one ExecuteUpdate over the same
/// filtered set the list endpoint shows — including the tsvector search filter.
/// Each test scopes itself with a unique token so it only touches its own rows.
/// </summary>
[Trait("Category", "Integration")]
public class MarkAllReadTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private static string Token() => "m" + Guid.NewGuid().ToString("N")[..10];

    private async Task<BookmarkDto> Create(HttpClient client, string url, string title)
    {
        var response = await client.PostAsJsonAsync("/api/bookmarks",
            new CreateBookmarkRequest(url, title, null, null, null, null, null));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<BookmarkDto>())!;
    }

    private async Task<int> MarkAllRead(HttpClient client, string filters, bool isRead = true)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/bookmarks/mark-read?{filters}", new MarkReadRequest(isRead));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("updated").GetInt32();
    }

    private async Task<PagedResult<BookmarkDto>> List(HttpClient client, string filters) =>
        (await factory.CreateAuthenticatedClient()
            .GetFromJsonAsync<PagedResult<BookmarkDto>>($"/api/bookmarks?{filters}"))!;

    [Fact]
    public async Task MarkAllRead_MarksEveryMatch_NotJustTheFirstPage()
    {
        var client = factory.CreateAuthenticatedClient();
        var token = Token();
        foreach (var n in new[] { "one", "two", "three" })
        {
            await Create(client, $"https://{token}-{n}.example.com", $"{token} {n}");
        }

        // pageSize is part of BookmarkQuery but must be ignored: the action spans all pages.
        var updated = await MarkAllRead(client, $"search={token}&pageSize=1");

        updated.Should().Be(3);
        (await List(client, $"search={token}&isRead=false")).Pagination.TotalItems.Should().Be(0);
    }

    [Fact]
    public async Task MarkAllRead_AffectsExactlyTheSetTheListEndpointShows()
    {
        var client = factory.CreateAuthenticatedClient();
        var token = Token();
        var other = Token();
        await Create(client, $"https://{token}-a.example.com", $"{token} match");
        await Create(client, $"https://{token}-b.example.com", $"{token} match");
        await Create(client, $"https://{other}-c.example.com", $"{other} unrelated");

        // Same filter string for both calls: what is marked must equal what is shown.
        var shown = await List(client, $"search={token}");
        var updated = await MarkAllRead(client, $"search={token}");

        updated.Should().Be(shown.Pagination.TotalItems);
        // The non-matching bookmark is untouched.
        (await List(client, $"search={other}&isRead=false")).Pagination.TotalItems.Should().Be(1);
    }

    [Fact]
    public async Task MarkAllRead_SkipsRowsAlreadyInTargetState()
    {
        var client = factory.CreateAuthenticatedClient();
        var token = Token();
        var already = await Create(client, $"https://{token}-x.example.com", $"{token} already");
        await Create(client, $"https://{token}-y.example.com", $"{token} fresh");

        (await client.PatchAsJsonAsync($"/api/bookmarks/{already.Id}/read", new MarkReadRequest(true)))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // Two match, but only one changes state — 'updated' counts real changes, which is why the
        // dialog's matched count and the toast's updated count legitimately differ.
        var updated = await MarkAllRead(client, $"search={token}");

        updated.Should().Be(1);
        (await List(client, $"search={token}&isRead=true")).Pagination.TotalItems.Should().Be(2);
    }

    [Fact]
    public async Task MarkAllRead_DoesNotTouchSoftDeletedBookmarks()
    {
        var client = factory.CreateAuthenticatedClient();
        var token = Token();
        var deleted = await Create(client, $"https://{token}-del.example.com", $"{token} deleted");
        await Create(client, $"https://{token}-live.example.com", $"{token} live");

        (await client.DeleteAsync($"/api/bookmarks/{deleted.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        // The soft-delete query filter must apply to ExecuteUpdate too.
        var updated = await MarkAllRead(client, $"search={token}");

        updated.Should().Be(1);
    }

    [Fact]
    public async Task MarkAllRead_UnreadFilter_WithReadTarget_MarksTheUnreadOnes()
    {
        var client = factory.CreateAuthenticatedClient();
        var token = Token();
        var read = await Create(client, $"https://{token}-r.example.com", $"{token} read");
        await Create(client, $"https://{token}-u.example.com", $"{token} unread");
        (await client.PatchAsJsonAsync($"/api/bookmarks/{read.Id}/read", new MarkReadRequest(true)))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // ?isRead=false filters the set; body {isRead:true} is the target. Same word, two roles.
        var updated = await MarkAllRead(client, $"search={token}&isRead=false", isRead: true);

        updated.Should().Be(1);
        (await List(client, $"search={token}&isRead=false")).Pagination.TotalItems.Should().Be(0);
    }

    [Fact]
    public async Task MarkAllRead_CanMarkUnread_InBulk()
    {
        var client = factory.CreateAuthenticatedClient();
        var token = Token();
        await Create(client, $"https://{token}-1.example.com", $"{token} one");
        await MarkAllRead(client, $"search={token}");

        // The endpoint takes isRead, so the unread direction exists even though no UI surfaces it.
        var updated = await MarkAllRead(client, $"search={token}", isRead: false);

        updated.Should().Be(1);
        (await List(client, $"search={token}&isRead=false")).Pagination.TotalItems.Should().Be(1);
    }

    [Fact]
    public async Task MarkAllRead_BumpsDateModified()
    {
        var client = factory.CreateAuthenticatedClient();
        var token = Token();
        var created = await Create(client, $"https://{token}-d.example.com", $"{token} dated");

        await Task.Delay(10);
        await MarkAllRead(client, $"search={token}");

        var after = await client.GetFromJsonAsync<BookmarkDto>($"/api/bookmarks/{created.Id}");
        after!.DateModified.Should().BeAfter(created.DateModified);
        after.IsRead.Should().BeTrue();
    }

    [Fact]
    public async Task MarkAllRead_WithNoFilters_AffectsTheEntireCollection()
    {
        var client = factory.CreateAuthenticatedClient();
        var token = Token();
        await Create(client, $"https://{token}-all1.example.com", $"{token} one");
        await Create(client, $"https://{token}-all2.example.com", $"{token} two");

        // No filter params at all — allowed by design; the UI's confirmation dialog is the guard.
        var updated = await MarkAllRead(client, "");

        updated.Should().BeGreaterThanOrEqualTo(2);
        (await List(client, $"search={token}&isRead=false")).Pagination.TotalItems.Should().Be(0);
    }

    [Fact]
    public async Task MarkAllRead_WithoutApiKey_IsUnauthorized()
    {
        var response = await factory.CreateClient()
            .PostAsJsonAsync("/api/bookmarks/mark-read", new MarkReadRequest(true));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

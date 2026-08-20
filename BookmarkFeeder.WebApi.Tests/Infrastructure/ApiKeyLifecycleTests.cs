using System.Net;
using System.Net.Http.Json;
using BookmarkFeeder.WebApi.Dtos;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BookmarkFeeder.WebApi.Tests.Infrastructure;

public class ApiKeyLifecycleTests(BookmarkApiFactory factory) : IClassFixture<BookmarkApiFactory>
{
    private static async Task<CreatedApiKeyDto> MintAsync(
        HttpClient owner, string name = "Test key", int? expiresInDays = null)
    {
        var response = await owner.PostAsJsonAsync(
            "/api/keys", new CreateApiKeyRequest(name, expiresInDays));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CreatedApiKeyDto>())!;
    }

    [Fact]
    public async Task AMintedKey_Authenticates()
    {
        var owner = factory.CreateAuthenticatedClient("auth0|lifecycle-works");
        var key = await MintAsync(owner);

        var response = await factory.CreateKeyClient(key.Key).GetAsync("/api/tags");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ThePlaintext_IsReturnedOnce_AndNeverAgain()
    {
        var owner = factory.CreateAuthenticatedClient("auth0|lifecycle-once");
        var key = await MintAsync(owner, "Shown once");

        key.Key.Should().StartWith("bmf_");

        var listed = await owner.GetFromJsonAsync<List<ApiKeyDto>>("/api/keys");
        var row = listed!.Single(k => k.Id == key.Id);

        // The list carries a display prefix only. This is the whole point of hashing: even the
        // owner cannot read the key back, so a database dump leaks nothing usable.
        row.Prefix.Should().NotBe(key.Key);
        key.Key.Should().StartWith(row.Prefix);

        var raw = await (await owner.GetAsync("/api/keys")).Content.ReadAsStringAsync();
        raw.Should().NotContain(key.Key, "the full key must never appear in a list response");
    }

    [Fact]
    public async Task ThePlaintext_IsNotStored()
    {
        var owner = factory.CreateAuthenticatedClient("auth0|lifecycle-hashed");
        var key = await MintAsync(owner);

        await using var db = factory.CreateContext();
        var stored = await db.ApiKeys.AsNoTracking().FirstAsync(k => k.Id == key.Id);

        stored.KeyHash.Should().NotBe(key.Key);
        stored.KeyHash.Should().HaveLength(64, "SHA-256 hex");
        stored.KeyHash.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public async Task ARevokedKey_StopsWorking()
    {
        var owner = factory.CreateAuthenticatedClient("auth0|lifecycle-revoke");
        var key = await MintAsync(owner);
        var keyClient = factory.CreateKeyClient(key.Key);

        (await keyClient.GetAsync("/api/tags")).StatusCode.Should().Be(HttpStatusCode.OK);

        var revoked = await owner.DeleteAsync($"/api/keys/{key.Id}");
        revoked.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await keyClient.GetAsync("/api/tags")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RevocationIsSoft_SoTheRowStaysVisible()
    {
        var owner = factory.CreateAuthenticatedClient("auth0|lifecycle-soft");
        var key = await MintAsync(owner, "Old laptop");

        await owner.DeleteAsync($"/api/keys/{key.Id}");

        var listed = await owner.GetFromJsonAsync<List<ApiKeyDto>>("/api/keys");
        var row = listed!.Single(k => k.Id == key.Id);

        // Kept, so the user can see what they turned off rather than watching it vanish.
        row.Name.Should().Be("Old laptop");
        row.RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task AnExpiredKey_IsRejected()
    {
        var owner = factory.CreateAuthenticatedClient("auth0|lifecycle-expired");
        var key = await MintAsync(owner, "Short lived", expiresInDays: 30);

        // Reach past the API to age it: there is no endpoint for time travel.
        await using (var db = factory.CreateContext())
        {
            var stored = await db.ApiKeys.FirstAsync(k => k.Id == key.Id);
            stored.ExpiresAt = DateTime.UtcNow.AddDays(-1);
            await db.SaveChangesAsync();
        }

        var response = await factory.CreateKeyClient(key.Key).GetAsync("/api/tags");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AUser_CanHoldSeveralKeys_RevokedIndependently()
    {
        var owner = factory.CreateAuthenticatedClient("auth0|lifecycle-many");
        var laptop = await MintAsync(owner, "Work laptop - Chrome");
        var desktop = await MintAsync(owner, "Home desktop - Edge");

        laptop.Key.Should().NotBe(desktop.Key);

        await owner.DeleteAsync($"/api/keys/{laptop.Id}");

        // Revoking one browser must not sign the others out - that is the reason for one key per
        // device rather than one shared key.
        (await factory.CreateKeyClient(laptop.Key).GetAsync("/api/tags"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await factory.CreateKeyClient(desktop.Key).GetAsync("/api/tags"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task OneUser_CannotSeeOrRevokeAnotherUsersKeys()
    {
        var alice = factory.CreateAuthenticatedClient("auth0|lifecycle-alice");
        var bob = factory.CreateAuthenticatedClient("auth0|lifecycle-bob");

        var aliceKey = await MintAsync(alice, "Alice laptop");
        await MintAsync(bob, "Bob laptop");

        var bobsList = await bob.GetFromJsonAsync<List<ApiKeyDto>>("/api/keys");
        bobsList!.Should().NotContain(k => k.Id == aliceKey.Id);

        // 404 rather than 403: Bob must not learn that this id exists.
        var attempt = await bob.DeleteAsync($"/api/keys/{aliceKey.Id}");
        attempt.StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await factory.CreateKeyClient(aliceKey.Key).GetAsync("/api/tags"))
            .StatusCode.Should().Be(HttpStatusCode.OK, "Alice's key must be untouched");
    }

    [Fact]
    public async Task AKey_ActsAsItsOwner_NotAsWhoeverHoldsIt()
    {
        var alice = factory.CreateAuthenticatedClient("auth0|lifecycle-owner-a");
        var key = await MintAsync(alice);

        await factory.CreateKeyClient(key.Key).PostAsJsonAsync("/api/bookmarks/batch", new
        {
            bookmarks = new[] { new { url = "https://owned-by-alice.test/", title = "Alice" } },
            skipDuplicates = true,
        });

        // The bookmark belongs to Alice, because the key resolves to exactly one owner.
        // Plain list rather than search: full-text search needs PostgreSQL, and this assertion is
        // about ownership, not ranking.
        var alicesBookmarks = await alice.GetStringAsync("/api/bookmarks?pageSize=100");
        alicesBookmarks.Should().Contain("owned-by-alice.test");

        var bob = factory.CreateAuthenticatedClient("auth0|lifecycle-owner-b");
        var bobsBookmarks = await bob.GetStringAsync("/api/bookmarks?pageSize=100");
        bobsBookmarks.Should().NotContain("owned-by-alice.test");
    }

    [Fact]
    public async Task AnUnnamedKey_IsRejected()
    {
        var owner = factory.CreateAuthenticatedClient("auth0|lifecycle-unnamed");

        var response = await owner.PostAsJsonAsync("/api/keys", new CreateApiKeyRequest("", null));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}

using System.Net;
using BookmarkFeeder.WebApi.Data.Configurations;
using BookmarkFeeder.WebApi.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BookmarkFeeder.WebApi.Tests.Infrastructure;

/// <summary>
/// Accounts are created just-in-time: Auth0 owns registration, so the first authenticated request
/// from an unknown subject is what brings a user row into existence.
/// </summary>
public class UserProvisioningTests
{
    [Fact]
    public async Task AnUnknownSubject_GetsAnAccountOnItsFirstRequest()
    {
        using var factory = new BookmarkApiFactory();
        var client = factory.CreateAuthenticatedClient("auth0|brand-new");

        (await client.GetAsync("/api/tags")).StatusCode.Should().Be(HttpStatusCode.OK);

        await using var db = factory.CreateContext();
        var user = await db.Users.SingleAsync(u => u.Auth0Sub == "auth0|brand-new");
        user.Email.Should().Be("auth0-brand-new@example.test");
    }

    [Fact]
    public async Task RepeatedRequests_DoNotCreateExtraAccounts()
    {
        using var factory = new BookmarkApiFactory();
        var client = factory.CreateAuthenticatedClient("auth0|repeat");

        for (var i = 0; i < 5; i++)
        {
            await client.GetAsync("/api/tags");
        }

        await using var db = factory.CreateContext();
        (await db.Users.CountAsync(u => u.Auth0Sub == "auth0|repeat")).Should().Be(1);
    }

    [Fact]
    public async Task ConcurrentFirstRequests_ProduceExactlyOneAccount()
    {
        using var factory = new BookmarkApiFactory();

        // Ten cold clients racing to provision the same subject. Without the gate (and the
        // unique-index catch behind it) this is where duplicate accounts would appear.
        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ =>
            factory.CreateAuthenticatedClient("auth0|stampede").GetAsync("/api/tags")));

        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);

        await using var db = factory.CreateContext();
        (await db.Users.CountAsync(u => u.Auth0Sub == "auth0|stampede")).Should().Be(1);
    }

    [Fact]
    public async Task TheFirstLogin_ClaimsThePreMultiUserCollection()
    {
        using var factory = new BookmarkApiFactory();

        // The state the migration leaves behind: an unclaimed placeholder owning everything that
        // existed before multi-user.
        await using (var seed = factory.CreateContext(UserConfiguration.PlaceholderUserId))
        {
            seed.Users.Add(new User
            {
                Id = UserConfiguration.PlaceholderUserId,
                Auth0Sub = null,
                DisplayName = "Pre-multi-user collection",
                CreatedAt = DateTime.UtcNow,
            });
            seed.Bookmarks.Add(new Bookmark
            {
                Id = Guid.NewGuid(),
                OwnerId = UserConfiguration.PlaceholderUserId,
                Url = "https://inherited.test/",
                Title = "Inherited bookmark",
                DateAdded = DateTime.UtcNow,
                DateModified = DateTime.UtcNow,
            });
            await seed.SaveChangesAsync();
        }

        var first = factory.CreateAuthenticatedClient("auth0|the-owner");
        var body = await first.GetStringAsync("/api/bookmarks?pageSize=100");

        // The whole point: signing in for the first time inherits the existing collection rather
        // than landing on an empty dashboard while the data sits orphaned.
        body.Should().Contain("inherited.test");

        await using var db = factory.CreateContext();
        var claimed = await db.Users.SingleAsync(u => u.Id == UserConfiguration.PlaceholderUserId);
        claimed.Auth0Sub.Should().Be("auth0|the-owner");
        (await db.Users.CountAsync()).Should().Be(1, "the placeholder was claimed, not duplicated");
    }

    [Fact]
    public async Task TheSecondUser_GetsAnEmptyCollection()
    {
        using var factory = new BookmarkApiFactory();

        await using (var seed = factory.CreateContext(UserConfiguration.PlaceholderUserId))
        {
            seed.Users.Add(new User
            {
                Id = UserConfiguration.PlaceholderUserId,
                Auth0Sub = null,
                CreatedAt = DateTime.UtcNow,
            });
            seed.Bookmarks.Add(new Bookmark
            {
                Id = Guid.NewGuid(),
                OwnerId = UserConfiguration.PlaceholderUserId,
                Url = "https://only-for-the-first.test/",
                Title = "First user's",
                DateAdded = DateTime.UtcNow,
                DateModified = DateTime.UtcNow,
            });
            await seed.SaveChangesAsync();
        }

        await factory.CreateAuthenticatedClient("auth0|first").GetAsync("/api/tags");

        var second = factory.CreateAuthenticatedClient("auth0|second");
        var body = await second.GetStringAsync("/api/bookmarks?pageSize=100");

        body.Should().NotContain("only-for-the-first.test",
            "the placeholder is claimed once; later accounts start empty");

        await using var db = factory.CreateContext();
        (await db.Users.CountAsync()).Should().Be(2);
    }

    private static async Task SeedUnclaimedCollectionAsync(BookmarkApiFactory factory)
    {
        await using var seed = factory.CreateContext(UserConfiguration.PlaceholderUserId);
        seed.Users.Add(new User
        {
            Id = UserConfiguration.PlaceholderUserId,
            Auth0Sub = null,
            CreatedAt = DateTime.UtcNow,
        });
        seed.Bookmarks.Add(new Bookmark
        {
            Id = Guid.NewGuid(),
            OwnerId = UserConfiguration.PlaceholderUserId,
            Url = "https://legacy.test/",
            Title = "Legacy",
            DateAdded = DateTime.UtcNow,
            DateModified = DateTime.UtcNow,
        });
        await seed.SaveChangesAsync();
    }

    [Fact]
    public async Task WithALegacyOwnerConfigured_AStrangerSigningInFirst_DoesNotGetTheCollection()
    {
        using var factory = new BookmarkApiFactory();
        factory.Settings["Auth0:LegacyOwner"] = "auth0|the-operator";
        await SeedUnclaimedCollectionAsync(factory);

        var stranger = factory.CreateAuthenticatedClient("auth0|someone-else");
        var body = await stranger.GetStringAsync("/api/bookmarks?pageSize=100");

        // Signup is open: being first must not be enough to take everything.
        body.Should().NotContain("legacy.test");
        await using var db = factory.CreateContext();
        (await db.Users.SingleAsync(u => u.Id == UserConfiguration.PlaceholderUserId))
            .Auth0Sub.Should().BeNull("the collection waits for its configured owner");

        // ...and the owner still gets it afterwards.
        var owner = factory.CreateAuthenticatedClient("auth0|the-operator");
        (await owner.GetStringAsync("/api/bookmarks?pageSize=100")).Should().Contain("legacy.test");
    }

    [Fact]
    public async Task TheLegacyOwner_CanBeMatchedByEmail()
    {
        using var factory = new BookmarkApiFactory();
        // Test tokens derive email from the subject: auth0|the-owner -> auth0-the-owner@example.test
        factory.Settings["Auth0:LegacyOwner"] = "AUTH0-the-owner@example.test";
        await SeedUnclaimedCollectionAsync(factory);

        var owner = factory.CreateAuthenticatedClient("auth0|the-owner");

        (await owner.GetStringAsync("/api/bookmarks?pageSize=100")).Should().Contain("legacy.test");
    }
}

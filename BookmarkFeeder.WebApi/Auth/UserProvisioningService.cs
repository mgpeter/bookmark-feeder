using System.Security.Claims;
using BookmarkFeeder.WebApi.Data;
using BookmarkFeeder.WebApi.Data.Configurations;
using BookmarkFeeder.WebApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace BookmarkFeeder.WebApi.Auth;

public interface IUserProvisioningService
{
    /// <summary>
    /// Maps a validated Auth0 subject to a BookmarkFeeder user id, creating the account on first
    /// sight. Auth0 owns registration, so this is the only place accounts come into existence.
    /// </summary>
    Task<Guid> ResolveAsync(ClaimsPrincipal principal, CancellationToken ct = default);
}

public sealed class UserProvisioningService(
    IDbContextFactory<BookmarkDbContext> contextFactory,
    IMemoryCache cache,
    IOptions<Auth0Options> options,
    TimeProvider timeProvider,
    ILogger<UserProvisioningService> logger) : IUserProvisioningService
{
    /// <summary>
    /// Collapses concurrent FIRST requests for the same subject into one provisioning attempt.
    /// Static because the service is scoped: the gate has to outlive the request. In a
    /// single-container deployment this is the whole race; the unique-index catch below is the
    /// cross-process backstop.
    /// </summary>
    private static readonly SemaphoreSlim ProvisioningGate = new(1, 1);

    private sealed record CacheEntry(Guid UserId, DateTimeOffset LastSeenWrittenAt);

    public async Task<Guid> ResolveAsync(ClaimsPrincipal principal, CancellationToken ct = default)
    {
        var sub = principal.FindFirstValue(AuthenticationExtensions.SubClaim)
            ?? throw new InvalidOperationException("The validated token has no 'sub' claim.");

        var cacheKey = $"auth0-user:{sub}";

        if (cache.TryGetValue<CacheEntry>(cacheKey, out var hit) && hit is not null)
        {
            // The hot path: no database access at all unless the throttle window has elapsed.
            await TouchLastSeenIfStaleAsync(cacheKey, hit, ct);
            return hit.UserId;
        }

        var userId = await LookupOrProvisionAsync(sub, principal, ct);

        cache.Set(
            cacheKey,
            new CacheEntry(userId, timeProvider.GetUtcNow()),
            TimeSpan.FromMinutes(options.Value.UserCacheMinutes));

        return userId;
    }

    private async Task<Guid> LookupOrProvisionAsync(string sub, ClaimsPrincipal principal, CancellationToken ct)
    {
        await using (var read = await contextFactory.CreateDbContextAsync(ct))
        {
            var existing = await read.Users.AsNoTracking()
                .Where(u => u.Auth0Sub == sub)
                .Select(u => u.Id)
                .FirstOrDefaultAsync(ct);

            if (existing != Guid.Empty)
            {
                return existing;
            }
        }

        await ProvisioningGate.WaitAsync(ct);
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(ct);

            // Re-check under the gate: another request may have provisioned while we queued.
            var again = await context.Users
                .Where(u => u.Auth0Sub == sub)
                .Select(u => u.Id)
                .FirstOrDefaultAsync(ct);

            if (again != Guid.Empty)
            {
                return again;
            }

            var email = principal.FindFirstValue("email");
            var name = principal.FindFirstValue("name")
                ?? principal.FindFirstValue("nickname")
                ?? email;
            var now = timeProvider.GetUtcNow().UtcDateTime;

            // Claim the migration placeholder if it is still unclaimed, so the collection that
            // existed before multi-user lands with the first person to sign in rather than being
            // orphaned. Exactly one login can win: the row is read and updated inside this gate,
            // and the next caller sees a non-null Auth0Sub and falls through to a normal insert.
            var placeholder = await context.Users.FirstOrDefaultAsync(
                u => u.Id == UserConfiguration.PlaceholderUserId && u.Auth0Sub == null, ct);

            if (placeholder is not null)
            {
                placeholder.Auth0Sub = sub;
                placeholder.Email = email;
                placeholder.DisplayName = name;
                placeholder.LastSeenAt = now;
                await context.SaveChangesAsync(ct);

                logger.LogInformation(
                    "Claimed the pre-multi-user collection for {Sub}", sub);
                return placeholder.Id;
            }

            var user = new User
            {
                // Time-ordered: better index locality than Guid.NewGuid().
                Id = Guid.CreateVersion7(),
                Auth0Sub = sub,
                Email = email,
                DisplayName = name,
                CreatedAt = now,
                LastSeenAt = now,
            };

            context.Users.Add(user);

            try
            {
                await context.SaveChangesAsync(ct);
                logger.LogInformation("Provisioned a new user for {Sub}", sub);
                return user.Id;
            }
            catch (DbUpdateException)
            {
                // Lost a cross-process race on IX_Users_Auth0Sub. The other writer won; adopt it.
                context.ChangeTracker.Clear();
                return await context.Users
                    .Where(u => u.Auth0Sub == sub)
                    .Select(u => u.Id)
                    .SingleAsync(ct);
            }
        }
        finally
        {
            ProvisioningGate.Release();
        }
    }

    /// <summary>
    /// Bookkeeping, throttled hard. Without the throttle this would be a database write on every
    /// authenticated request for a value nobody reads more than once a session.
    /// </summary>
    private async Task TouchLastSeenIfStaleAsync(string cacheKey, CacheEntry entry, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        if (now - entry.LastSeenWrittenAt < TimeSpan.FromMinutes(options.Value.LastSeenThrottleMinutes))
        {
            return;
        }

        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(ct);
            var user = await context.Users.FirstOrDefaultAsync(u => u.Id == entry.UserId, ct);
            if (user is not null)
            {
                user.LastSeenAt = now.UtcDateTime;
                await context.SaveChangesAsync(ct);
            }

            cache.Set(
                cacheKey,
                entry with { LastSeenWrittenAt = now },
                TimeSpan.FromMinutes(options.Value.UserCacheMinutes));
        }
        catch (DbUpdateException)
        {
            // Never fail a real request over a LastSeenAt write.
        }
    }
}

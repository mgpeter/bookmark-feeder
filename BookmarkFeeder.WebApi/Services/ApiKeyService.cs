using System.Security.Cryptography;
using System.Text;
using BookmarkFeeder.WebApi.Data;
using BookmarkFeeder.WebApi.Dtos;
using BookmarkFeeder.WebApi.Models;
using Microsoft.EntityFrameworkCore;

namespace BookmarkFeeder.WebApi.Services;

public interface IApiKeyService
{
    Task<IReadOnlyList<ApiKeyDto>> GetAllAsync(Guid ownerId, CancellationToken ct = default);

    /// <summary>Mints a key. The returned plaintext is the only copy that will ever exist.</summary>
    Task<CreatedApiKeyDto> CreateAsync(Guid ownerId, CreateApiKeyRequest request, CancellationToken ct = default);

    Task<bool> RevokeAsync(Guid ownerId, Guid id, CancellationToken ct = default);

    /// <summary>
    /// Resolves a presented key to its owner, or null if it is unknown, revoked or expired.
    /// Called by ApiKeyAuthenticationHandler on every extension request.
    /// </summary>
    Task<ApiKeyValidationResult?> ValidateAsync(string presentedKey, CancellationToken ct = default);
}

public record ApiKeyValidationResult(Guid KeyId, Guid OwnerId);

public class ApiKeyService(
    IDbContextFactory<BookmarkDbContext> contextFactory,
    TimeProvider timeProvider,
    ILogger<ApiKeyService> logger) : IApiKeyService
{
    /// <summary>Identifies our keys on sight, in a log or a support ticket.</summary>
    public const string KeyPrefix = "bmf_";

    /// <summary>Characters of the secret kept in clear for lookup and for display.</summary>
    private const int PrefixLength = 8;

    /// <summary>256 bits. Enough that guessing is not a threat model.</summary>
    private const int SecretBytes = 32;

    /// <summary>Only write LastUsedAt this often; a sync would otherwise cost a write per request.</summary>
    private static readonly TimeSpan LastUsedThrottle = TimeSpan.FromHours(1);

    public async Task<IReadOnlyList<ApiKeyDto>> GetAllAsync(Guid ownerId, CancellationToken ct = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(ct);

        // Explicitly scoped: ApiKey carries no "Owner" query filter, because the authentication
        // handler has to find a key before the current user is known.
        return await context.ApiKeys
            .AsNoTracking()
            .Where(k => k.OwnerId == ownerId)
            .OrderByDescending(k => k.CreatedAt)
            .Select(k => new ApiKeyDto(
                k.Id,
                k.Name,
                KeyPrefix + k.Prefix,
                k.CreatedAt,
                k.LastUsedAt,
                k.ExpiresAt,
                k.RevokedAt))
            .ToListAsync(ct);
    }

    public async Task<CreatedApiKeyDto> CreateAsync(
        Guid ownerId, CreateApiKeyRequest request, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        // base64url of 32 random bytes: no padding, no characters that need escaping in a header.
        var secret = Base64UrlEncode(RandomNumberGenerator.GetBytes(SecretBytes));
        var fullKey = KeyPrefix + secret;

        var entity = new ApiKey
        {
            Id = Guid.CreateVersion7(),
            OwnerId = ownerId,
            Name = string.IsNullOrWhiteSpace(request.Name) ? "Unnamed key" : request.Name.Trim(),
            Prefix = secret[..PrefixLength],
            KeyHash = Hash(fullKey),
            CreatedAt = now,
            ExpiresAt = request.ExpiresInDays is > 0 ? now.AddDays(request.ExpiresInDays.Value) : null,
        };

        await using var context = await contextFactory.CreateDbContextAsync(ct);
        context.ApiKeys.Add(entity);
        await context.SaveChangesAsync(ct);

        logger.LogInformation("Minted API key {KeyId} for owner {OwnerId}", entity.Id, ownerId);

        // The ONLY time the plaintext leaves this method. It is not stored and cannot be recovered.
        return new CreatedApiKeyDto(
            entity.Id,
            entity.Name,
            KeyPrefix + entity.Prefix,
            entity.CreatedAt,
            entity.ExpiresAt,
            fullKey);
    }

    public async Task<bool> RevokeAsync(Guid ownerId, Guid id, CancellationToken ct = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(ct);

        // Owner in the predicate, so one user cannot revoke another's key by guessing an id.
        var key = await context.ApiKeys
            .FirstOrDefaultAsync(k => k.Id == id && k.OwnerId == ownerId, ct);

        if (key is null || key.RevokedAt is not null)
        {
            return false;
        }

        // Soft: keeps the name and LastUsedAt visible so the user can see what they turned off.
        key.RevokedAt = timeProvider.GetUtcNow().UtcDateTime;
        await context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ApiKeyValidationResult?> ValidateAsync(string presentedKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(presentedKey) || !presentedKey.StartsWith(KeyPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var secret = presentedKey[KeyPrefix.Length..];
        if (secret.Length < PrefixLength)
        {
            return null;
        }

        var lookupPrefix = secret[..PrefixLength];
        var presentedHash = Hash(presentedKey);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        await using var context = await contextFactory.CreateDbContextAsync(ct);

        // Narrow by the clear-text prefix (indexed), then verify the hash. Prefix collisions are
        // possible but rare, so this is a short candidate list rather than a single row.
        var candidates = await context.ApiKeys
            .Where(k => k.Prefix == lookupPrefix)
            .ToListAsync(ct);

        foreach (var candidate in candidates)
        {
            if (!FixedTimeEquals(candidate.KeyHash, presentedHash))
            {
                continue;
            }

            if (!candidate.IsActive(now))
            {
                // Revoked or expired. Deliberately indistinguishable from "unknown key" to the
                // caller - the 401 body must not confirm that a key once existed.
                return null;
            }

            await TouchLastUsedAsync(context, candidate, now, ct);
            return new ApiKeyValidationResult(candidate.Id, candidate.OwnerId);
        }

        return null;
    }

    private static async Task TouchLastUsedAsync(
        BookmarkDbContext context, ApiKey key, DateTime now, CancellationToken ct)
    {
        if (key.LastUsedAt is not null && now - key.LastUsedAt.Value < LastUsedThrottle)
        {
            return;
        }

        key.LastUsedAt = now;
        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Bookkeeping only. Losing a LastUsedAt write must never fail the request it decorates.
        }
    }

    /// <summary>
    /// SHA-256, deliberately NOT bcrypt/argon2. A 256-bit random key has no guessable structure,
    /// so an attacker cannot do better than brute force regardless of hash cost; a slow KDF would
    /// add latency to every single API request and buy nothing. That reasoning applies to random
    /// tokens, not to passwords - do not "upgrade" this.
    /// </summary>
    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static bool FixedTimeEquals(string a, string b)
    {
        var ba = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        return ba.Length == bb.Length && CryptographicOperations.FixedTimeEquals(ba, bb);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

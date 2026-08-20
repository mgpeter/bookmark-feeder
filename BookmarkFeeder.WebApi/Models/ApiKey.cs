namespace BookmarkFeeder.WebApi.Models;

/// <summary>
/// A long-lived credential the user mints in the web app and pastes into the browser extension.
/// Sync-scoped: see ApiKeyScopeFilter — a key reaches an explicit allow-list of endpoints and
/// nothing else, so one leaked from a browser profile cannot delete a collection.
/// </summary>
public class ApiKey
{
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }
    public User Owner { get; set; } = null!;

    /// <summary>User-supplied label, e.g. "Work laptop - Chrome". Purely for the revoke UI.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// First 8 characters of the secret, stored in clear. Indexed, so validation narrows to a
    /// handful of rows before doing any hashing, and shown in the UI as the key's identity
    /// ("bmf_7Kd9…") since the full value is never retrievable again.
    /// </summary>
    public string Prefix { get; set; } = string.Empty;

    /// <summary>
    /// SHA-256 of the full key, hex-encoded. UNIQUE — that constraint is what makes keys globally
    /// unique and therefore unambiguously attributable to one owner.
    /// </summary>
    public string KeyHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    /// <summary>Throttled to at most one write per hour; otherwise every sync costs a DB write.</summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>Null means it never expires. Chosen at creation.</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>Soft revocation, so the row's history and LastUsedAt survive being turned off.</summary>
    public DateTime? RevokedAt { get; set; }

    public bool IsActive(DateTime utcNow) =>
        RevokedAt is null && (ExpiresAt is null || ExpiresAt > utcNow);
}

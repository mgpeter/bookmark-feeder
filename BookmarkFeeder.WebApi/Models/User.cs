namespace BookmarkFeeder.WebApi.Models;

/// <summary>
/// An account. Created just-in-time on the first authenticated request from an Auth0 identity —
/// there is no sign-up endpoint, because Auth0 owns registration.
/// </summary>
public class User
{
    public Guid Id { get; set; }

    /// <summary>
    /// The Auth0 `sub` claim ("auth0|abc", "google-oauth2|123"). NULL only for the single migration
    /// placeholder that owns the pre-multi-user collection; the first successful login claims that
    /// row rather than creating a new one, so the existing bookmarks are not orphaned.
    /// </summary>
    public string? Auth0Sub { get; set; }

    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Last authenticated request. Throttled — NOT written on every request.</summary>
    public DateTime? LastSeenAt { get; set; }

    public ICollection<Bookmark> Bookmarks { get; set; } = new List<Bookmark>();
    public ICollection<Tag> Tags { get; set; } = new List<Tag>();
    public ICollection<Category> Categories { get; set; } = new List<Category>();
    public ICollection<SavedSearch> SavedSearches { get; set; } = new List<SavedSearch>();
    public ICollection<ApiKey> ApiKeys { get; set; } = new List<ApiKey>();
}

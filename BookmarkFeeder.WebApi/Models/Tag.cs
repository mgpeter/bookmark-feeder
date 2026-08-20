namespace BookmarkFeeder.WebApi.Models;

public class Tag : IOwnedEntity
{
    public Guid Id { get; set; }

    /// <summary>The account this belongs to. Enforced by the "Owner" global query filter.</summary>
    public Guid OwnerId { get; set; }
    public User Owner { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public string? Color { get; set; }
    public DateTime DateCreated { get; set; }

    public ICollection<BookmarkTag> BookmarkTags { get; set; } = new List<BookmarkTag>();
}

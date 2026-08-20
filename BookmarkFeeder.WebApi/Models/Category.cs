namespace BookmarkFeeder.WebApi.Models;

public class Category : IOwnedEntity
{
    public Guid Id { get; set; }

    /// <summary>The account this belongs to. Enforced by the "Owner" global query filter.</summary>
    public Guid OwnerId { get; set; }
    public User Owner { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public Guid? ParentCategoryId { get; set; }
    public string? Description { get; set; }
    public DateTime DateCreated { get; set; }
    
    public Category? ParentCategory { get; set; }
    public ICollection<Category> SubCategories { get; set; } = new List<Category>();
    public ICollection<Bookmark> Bookmarks { get; set; } = new List<Bookmark>();
}
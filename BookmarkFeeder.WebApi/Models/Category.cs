namespace BookmarkFeeder.WebApi.Models;

public class Category
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? ParentCategoryId { get; set; }
    public string? Description { get; set; }
    public DateTime DateCreated { get; set; }
    
    public Category? ParentCategory { get; set; }
    public ICollection<Category> SubCategories { get; set; } = new List<Category>();
    public ICollection<Bookmark> Bookmarks { get; set; } = new List<Bookmark>();
}
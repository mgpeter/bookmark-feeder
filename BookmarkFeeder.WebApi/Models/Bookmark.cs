namespace BookmarkFeeder.WebApi.Models;

public class Bookmark
{
    public Guid Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? FaviconUrl { get; set; }
    public string? SourceFolder { get; set; }
    public bool IsRead { get; set; }
    public DateTime DateAdded { get; set; }
    public DateTime DateModified { get; set; }
    public bool IsDeleted { get; set; }

    public Guid? CategoryId { get; set; }
    public Category? Category { get; set; }

    public ICollection<BookmarkTag> BookmarkTags { get; set; } = new List<BookmarkTag>();
}

namespace BookmarkFeeder.Data.Models;

public class BookmarkTag
{
    public int BookmarkId { get; set; }
    public int TagId { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public Bookmark Bookmark { get; set; } = null!;
    public Tag Tag { get; set; } = null!;
}
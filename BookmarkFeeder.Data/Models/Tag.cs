using System.ComponentModel.DataAnnotations;

namespace BookmarkFeeder.Data.Models;

public class Tag
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string NormalizedName { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public ICollection<BookmarkTag> BookmarkTags { get; set; } = new List<BookmarkTag>();
    public ICollection<Bookmark> Bookmarks { get; set; } = new List<Bookmark>();
}
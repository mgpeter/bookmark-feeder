using System.ComponentModel.DataAnnotations;

namespace BookmarkFeeder.Data.Models;

public class Bookmark
{
    public int Id { get; set; }

    [Required]
    [MaxLength(2000)]
    public string Url { get; set; } = null!;

    [Required]
    [MaxLength(500)]
    public string Title { get; set; } = null!;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(200)]
    public string? SourceFolder { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public ICollection<BookmarkTag> BookmarkTags { get; set; } = new List<BookmarkTag>();
    public ICollection<Tag> Tags { get; set; } = new List<Tag>();
}
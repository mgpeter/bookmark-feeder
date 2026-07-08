namespace BookmarkFeeder.WebApi.Models;

public class Tag
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public string? Color { get; set; }
    public DateTime DateCreated { get; set; }

    public ICollection<BookmarkTag> BookmarkTags { get; set; } = new List<BookmarkTag>();
}

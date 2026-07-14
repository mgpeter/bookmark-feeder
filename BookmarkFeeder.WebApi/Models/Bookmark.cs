using NpgsqlTypes;

namespace BookmarkFeeder.WebApi.Models;

public class Bookmark
{
    public Guid Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>The resolved remote favicon URL, discovered from the site's own origin.</summary>
    public string? FaviconUrl { get; set; }

    /// <summary>
    /// When favicon discovery was last attempted, successful or not. Null means never tried,
    /// which is what the backfill looks for; setting it on failure stops a site with no
    /// discoverable favicon from being re-fetched forever.
    /// </summary>
    public DateTime? FaviconFetchedAt { get; set; }

    public string? SourceFolder { get; set; }
    public bool IsRead { get; set; }
    public DateTime DateAdded { get; set; }
    public DateTime DateModified { get; set; }
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Weighted full-text index over Title/Description/Url, generated and maintained by
    /// PostgreSQL. Never assigned in application code, and not exposed on BookmarkDto.
    /// </summary>
    public NpgsqlTsVector SearchVector { get; set; } = null!;

    public Guid? CategoryId { get; set; }
    public Category? Category { get; set; }

    public ICollection<BookmarkTag> BookmarkTags { get; set; } = new List<BookmarkTag>();
}

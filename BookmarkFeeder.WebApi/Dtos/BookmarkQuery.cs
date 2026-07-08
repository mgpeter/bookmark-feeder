namespace BookmarkFeeder.WebApi.Dtos;

/// <summary>Query-string parameters for GET /api/bookmarks (bound via [AsParameters]).</summary>
public class BookmarkQuery
{
    public int? Page { get; set; }
    public int? PageSize { get; set; }

    /// <summary>Free-text search over title, url and description.</summary>
    public string? Search { get; set; }

    /// <summary>Comma-separated tag names; matches bookmarks having ANY of them.</summary>
    public string? Tags { get; set; }

    /// <summary>Comma-separated category ids; matches bookmarks in ANY of them.</summary>
    public string? Categories { get; set; }

    /// <summary>Matches bookmarks whose source folder starts with this value.</summary>
    public string? SourceFolder { get; set; }

    public bool? IsRead { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }

    /// <summary>One of: dateAdded, dateModified, title, url.</summary>
    public string? SortBy { get; set; }

    /// <summary>asc or desc.</summary>
    public string? SortOrder { get; set; }
}

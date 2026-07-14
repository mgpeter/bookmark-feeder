using BookmarkFeeder.WebApi.Common;

namespace BookmarkFeeder.WebApi.Dtos;

/// <summary>A tag as embedded in a bookmark response.</summary>
public record TagRefDto(Guid Id, string Name, string NormalizedName, string? Color);

/// <summary>A category reference as embedded in a bookmark response.</summary>
public record CategoryRefDto(Guid Id, string Name);

public record BookmarkDto(
    Guid Id,
    string Url,
    string Title,
    string? Description,
    string? FaviconUrl,
    string? SourceFolder,
    bool IsRead,
    DateTime DateAdded,
    DateTime DateModified,
    IReadOnlyList<TagRefDto> Tags,
    IReadOnlyList<CategoryRefDto> Categories);

// ----- Facets -----

/// <summary>One facet bucket: a tag or category and how many results carry it.</summary>
public record FacetItemDto(Guid Id, string Name, int Count);

/// <summary>Tag and category breakdowns of the current result set.</summary>
public record BookmarkFacetsDto(
    IReadOnlyList<FacetItemDto> Tags,
    IReadOnlyList<FacetItemDto> Categories);

/// <summary>
/// The bookmark list: a page of results plus, when a search or filter is narrowing them,
/// the facet counts for the whole matching set.
/// </summary>
public record BookmarkListResult(
    IReadOnlyList<BookmarkDto> Data,
    PaginationDto Pagination,
    BookmarkFacetsDto? Facets)
    : PagedResult<BookmarkDto>(Data, Pagination);

public record CreateBookmarkRequest(
    string Url,
    string Title,
    string? Description,
    string? FaviconUrl,
    string? SourceFolder,
    Guid? CategoryId,
    string[]? Tags);

public record UpdateBookmarkRequest(
    string? Title,
    string? Description,
    string? FaviconUrl,
    bool? IsRead,
    Guid? CategoryId,
    string[]? Tags);

public record MarkReadRequest(bool IsRead);

// ----- Batch (browser-extension sync contract) -----

/// <summary>One item in a batch sync. <paramref name="DateAdded"/> is Chrome epoch milliseconds.</summary>
public record BatchBookmarkItem(
    string Url,
    string Title,
    string? Description,
    string? SourceFolder,
    long? DateAdded);

public record BatchCreateRequest(
    List<BatchBookmarkItem> Bookmarks,
    string[]? DefaultTags = null,
    bool SkipDuplicates = true);

public record BatchCreatedItem(Guid Id, string Url, string Status = "created");
public record BatchSkippedItem(string Url, string Status = "duplicate");
public record BatchErrorItem(string Url, string Message);

public record BatchSummary(int Total, int Created, int Skipped, int Errors);

public record BatchResultDto(
    List<BatchCreatedItem> Created,
    List<BatchSkippedItem> Skipped,
    List<BatchErrorItem> Errors,
    BatchSummary Summary);

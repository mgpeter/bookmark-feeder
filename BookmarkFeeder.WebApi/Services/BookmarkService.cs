using BookmarkFeeder.WebApi.Common;
using BookmarkFeeder.WebApi.Data;
using BookmarkFeeder.WebApi.Dtos;
using BookmarkFeeder.WebApi.Models;
using Microsoft.EntityFrameworkCore;

namespace BookmarkFeeder.WebApi.Services;

public enum BookmarkError
{
    None,
    Duplicate,
    InvalidCategory
}

public interface IBookmarkService
{
    Task<PagedResult<BookmarkDto>> GetBookmarksAsync(BookmarkQuery query, CancellationToken ct = default);
    Task<BookmarkDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<(BookmarkDto? Dto, BookmarkError Error)> CreateAsync(CreateBookmarkRequest request, CancellationToken ct = default);
    Task<(BookmarkDto? Dto, BookmarkError Error)> UpdateAsync(Guid id, UpdateBookmarkRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<BookmarkDto?> SetReadAsync(Guid id, bool isRead, CancellationToken ct = default);

    /// <summary>
    /// Sets the read state of every bookmark matching <paramref name="query"/>'s filters, across
    /// all pages. Returns the number of rows whose state actually changed.
    /// </summary>
    Task<int> MarkAllReadAsync(BookmarkQuery query, bool isRead, CancellationToken ct = default);
    Task<BatchResultDto> CreateBatchAsync(BatchCreateRequest request, CancellationToken ct = default);
}

public class BookmarkService(IDbContextFactory<BookmarkDbContext> contextFactory) : IBookmarkService
{
    public async Task<PagedResult<BookmarkDto>> GetBookmarksAsync(BookmarkQuery query, CancellationToken ct = default)
    {
        var page = query.Page is > 0 ? query.Page.Value : 1;
        var pageSize = Math.Clamp(query.PageSize ?? 20, 1, 100);

        await using var context = await contextFactory.CreateDbContextAsync(ct);

        IQueryable<Bookmark> q = context.Bookmarks
            .AsNoTracking()
            .Include(b => b.Category)
            .Include(b => b.BookmarkTags)
                .ThenInclude(bt => bt.Tag);

        var searchTerm = query.Search?.Trim();
        q = ApplyFilters(context, q, query, searchTerm);

        var total = await q.CountAsync(ct);

        q = ApplySort(q, query.SortBy, query.SortOrder, searchTerm);

        var items = await q
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var data = items.Select(ToDto).ToList();
        return PagedResult<BookmarkDto>.Create(data, page, pageSize, total);
    }

    public async Task<BookmarkDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(ct);
        var bookmark = await LoadWithGraph(context.Bookmarks.AsNoTracking())
            .FirstOrDefaultAsync(b => b.Id == id, ct);
        return bookmark is null ? null : ToDto(bookmark);
    }

    public async Task<(BookmarkDto? Dto, BookmarkError Error)> CreateAsync(CreateBookmarkRequest request, CancellationToken ct = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(ct);

        var url = request.Url.Trim();
        var exists = await context.Bookmarks.IgnoreQueryFilters().AnyAsync(b => b.Url == url, ct);
        if (exists)
        {
            return (null, BookmarkError.Duplicate);
        }

        if (request.CategoryId is { } categoryId && !await context.Categories.AnyAsync(c => c.Id == categoryId, ct))
        {
            return (null, BookmarkError.InvalidCategory);
        }

        var now = DateTime.UtcNow;
        var bookmark = new Bookmark
        {
            Id = Guid.NewGuid(),
            Url = url,
            Title = string.IsNullOrWhiteSpace(request.Title) ? url : request.Title.Trim(),
            Description = request.Description,
            FaviconUrl = request.FaviconUrl,
            SourceFolder = request.SourceFolder,
            CategoryId = request.CategoryId,
            DateAdded = now,
            DateModified = now
        };

        var tags = await ResolveTagsAsync(context, request.Tags ?? [], ct);
        foreach (var tag in tags)
        {
            bookmark.BookmarkTags.Add(new BookmarkTag { Bookmark = bookmark, Tag = tag, DateAssigned = now });
        }

        context.Bookmarks.Add(bookmark);
        await context.SaveChangesAsync(ct);

        return ((await GetByIdAsync(bookmark.Id, ct))!, BookmarkError.None);
    }

    public async Task<(BookmarkDto? Dto, BookmarkError Error)> UpdateAsync(Guid id, UpdateBookmarkRequest request, CancellationToken ct = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(ct);
        var bookmark = await context.Bookmarks
            .Include(b => b.BookmarkTags)
            .FirstOrDefaultAsync(b => b.Id == id, ct);

        if (bookmark is null)
        {
            return (null, BookmarkError.None);
        }

        if (request.CategoryId is { } categoryId && !await context.Categories.AnyAsync(c => c.Id == categoryId, ct))
        {
            return (null, BookmarkError.InvalidCategory);
        }

        if (request.Title is not null) bookmark.Title = request.Title.Trim();
        if (request.Description is not null) bookmark.Description = request.Description;
        if (request.FaviconUrl is not null) bookmark.FaviconUrl = request.FaviconUrl;
        if (request.IsRead.HasValue) bookmark.IsRead = request.IsRead.Value;
        if (request.CategoryId.HasValue) bookmark.CategoryId = request.CategoryId;

        if (request.Tags is not null)
        {
            bookmark.BookmarkTags.Clear();
            var tags = await ResolveTagsAsync(context, request.Tags, ct);
            var now = DateTime.UtcNow;
            foreach (var tag in tags)
            {
                bookmark.BookmarkTags.Add(new BookmarkTag { BookmarkId = bookmark.Id, Tag = tag, DateAssigned = now });
            }
        }

        bookmark.DateModified = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);

        return ((await GetByIdAsync(id, ct))!, BookmarkError.None);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(ct);
        var bookmark = await context.Bookmarks.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (bookmark is null)
        {
            return false;
        }

        bookmark.IsDeleted = true;
        bookmark.DateModified = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<BookmarkDto?> SetReadAsync(Guid id, bool isRead, CancellationToken ct = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(ct);
        var bookmark = await context.Bookmarks.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (bookmark is null)
        {
            return null;
        }

        bookmark.IsRead = isRead;
        bookmark.DateModified = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task<BatchResultDto> CreateBatchAsync(BatchCreateRequest request, CancellationToken ct = default)
    {
        var created = new List<BatchCreatedItem>();
        var skipped = new List<BatchSkippedItem>();
        var errors = new List<BatchErrorItem>();

        var items = request.Bookmarks ?? [];

        await using var context = await contextFactory.CreateDbContextAsync(ct);

        // Pre-load URLs that already exist (ignoring the soft-delete filter, because the unique
        // index still covers soft-deleted rows) to detect duplicates in a single round-trip.
        var incomingUrls = items
            .Select(i => i.Url?.Trim())
            .Where(u => !string.IsNullOrEmpty(u))
            .Select(u => u!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existingUrls = incomingUrls.Count == 0
            ? []
            : await context.Bookmarks.IgnoreQueryFilters()
                .Where(b => incomingUrls.Contains(b.Url))
                .Select(b => b.Url)
                .ToListAsync(ct);

        var known = new HashSet<string>(existingUrls, StringComparer.OrdinalIgnoreCase);
        var seenInBatch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var defaultTags = await ResolveTagsAsync(context, request.DefaultTags ?? [], ct);

        foreach (var item in items)
        {
            var url = item.Url?.Trim();
            if (string.IsNullOrEmpty(url))
            {
                errors.Add(new BatchErrorItem(item.Url ?? string.Empty, "URL is required."));
                continue;
            }

            if (!seenInBatch.Add(url) || known.Contains(url))
            {
                if (request.SkipDuplicates)
                {
                    skipped.Add(new BatchSkippedItem(url));
                }
                else
                {
                    errors.Add(new BatchErrorItem(url, "Duplicate URL."));
                }
                continue;
            }

            var dateAdded = item.DateAdded.HasValue
                ? DateTime.UnixEpoch.AddMilliseconds(item.DateAdded.Value)
                : DateTime.UtcNow;

            var bookmark = new Bookmark
            {
                Id = Guid.NewGuid(),
                Url = url,
                Title = string.IsNullOrWhiteSpace(item.Title) ? url : item.Title.Trim(),
                Description = item.Description,
                SourceFolder = item.SourceFolder,
                DateAdded = dateAdded,
                DateModified = DateTime.UtcNow
            };

            foreach (var tag in defaultTags)
            {
                bookmark.BookmarkTags.Add(new BookmarkTag { Bookmark = bookmark, Tag = tag, DateAssigned = bookmark.DateModified });
            }

            context.Bookmarks.Add(bookmark);
            created.Add(new BatchCreatedItem(bookmark.Id, url));
        }

        if (created.Count > 0)
        {
            await context.SaveChangesAsync(ct);
        }

        var summary = new BatchSummary(items.Count, created.Count, skipped.Count, errors.Count);
        return new BatchResultDto(created, skipped, errors, summary);
    }

    // ----- helpers -----

    private static IQueryable<Bookmark> LoadWithGraph(IQueryable<Bookmark> source) =>
        source
            .Include(b => b.Category)
            .Include(b => b.BookmarkTags)
                .ThenInclude(bt => bt.Tag);

    public async Task<int> MarkAllReadAsync(BookmarkQuery query, bool isRead, CancellationToken ct = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(ct);

        // No Includes: ExecuteUpdate cannot use them and the navigations aren't needed. Page and
        // sort are ignored — the action spans every match by design, and ordering an update is
        // meaningless. The soft-delete query filter still applies automatically.
        var q = ApplyFilters(context, context.Bookmarks, query, query.Search?.Trim());

        // Skip rows already in the target state so their DateModified isn't churned. The return
        // value is therefore rows actually changed, which can be lower than the matched total.
        q = q.Where(b => b.IsRead != isRead);

        var now = DateTime.UtcNow;
        return await q.ExecuteUpdateAsync(
            s => s.SetProperty(b => b.IsRead, isRead)
                  .SetProperty(b => b.DateModified, now),
            ct);
    }

    /// <summary>
    /// Applies every narrowing filter in <paramref name="query"/> (search, tags, categories,
    /// source folder, read state, dates). Paging and sorting are deliberately excluded.
    /// </summary>
    /// <remarks>
    /// Shared by the list endpoint and the bulk mark-read endpoint on purpose: if the two composed
    /// their filters separately, the set marked read could drift from the set shown on screen.
    /// Any filter added here must therefore make sense for both.
    /// </remarks>
    private static IQueryable<Bookmark> ApplyFilters(
        BookmarkDbContext context, IQueryable<Bookmark> q, BookmarkQuery query, string? searchTerm)
    {
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            // Matches the weighted tsvector (title/description/url) or a tag name. Tag names are
            // deliberately outside the vector, so they contribute no rank — a tag-only hit sorts
            // below any text hit.
            //
            // The two are UNIONed rather than OR'd. A disjunction spanning tables
            // (SearchVector @@ q OR EXISTS (tag...)) makes the planner give up on the GIN index
            // and sequentially scan every bookmark — which would defeat the index this search is
            // built on. UNION lets each branch use its own index (measured on 434 rows: plan cost
            // 9678 -> 59). Matching on ids keeps the Includes on the outer query.
            //
            // websearch_to_tsquery parses user syntax ("quoted", OR, -negated) and tolerates junk
            // rather than throwing, so raw input passes straight through.
            var textMatches = context.Bookmarks
                .Where(b => b.SearchVector.Matches(EF.Functions.WebSearchToTsQuery("english", searchTerm)))
                .Select(b => b.Id);

            var tagMatches = context.Bookmarks
                .Where(b => b.BookmarkTags.Any(bt => EF.Functions.ILike(bt.Tag.Name, $"%{searchTerm}%")))
                .Select(b => b.Id);

            var matchingIds = textMatches.Union(tagMatches);
            q = q.Where(b => matchingIds.Contains(b.Id));
        }

        if (!string.IsNullOrWhiteSpace(query.Tags))
        {
            var tagNames = SplitCsv(query.Tags).Select(t => t.ToLowerInvariant()).ToList();
            if (tagNames.Count > 0)
            {
                q = q.Where(b => b.BookmarkTags.Any(bt => tagNames.Contains(bt.Tag.NormalizedName)));
            }
        }

        if (!string.IsNullOrWhiteSpace(query.Categories))
        {
            var categoryIds = SplitCsv(query.Categories)
                .Select(c => Guid.TryParse(c, out var g) ? g : (Guid?)null)
                .Where(g => g.HasValue)
                .Select(g => g!.Value)
                .ToList();
            if (categoryIds.Count > 0)
            {
                q = q.Where(b => b.CategoryId != null && categoryIds.Contains(b.CategoryId.Value));
            }
        }

        if (!string.IsNullOrWhiteSpace(query.SourceFolder))
        {
            var folder = query.SourceFolder.Trim();
            q = q.Where(b => b.SourceFolder != null && b.SourceFolder.StartsWith(folder));
        }

        if (query.IsRead.HasValue)
        {
            q = q.Where(b => b.IsRead == query.IsRead.Value);
        }

        if (query.DateFrom.HasValue)
        {
            var from = DateTime.SpecifyKind(query.DateFrom.Value, DateTimeKind.Utc);
            q = q.Where(b => b.DateAdded >= from);
        }

        if (query.DateTo.HasValue)
        {
            var to = DateTime.SpecifyKind(query.DateTo.Value, DateTimeKind.Utc);
            q = q.Where(b => b.DateAdded <= to);
        }

        return q;
    }

    private static IQueryable<Bookmark> ApplySort(
        IQueryable<Bookmark> q, string? sortBy, string? sortOrder, string? searchTerm)
    {
        var descending = !string.Equals(sortOrder, "asc", StringComparison.OrdinalIgnoreCase);
        var hasSearch = !string.IsNullOrWhiteSpace(searchTerm);

        // A search with no explicit sort ranks by relevance rather than recency.
        var effective = string.IsNullOrWhiteSpace(sortBy) && hasSearch
            ? "relevance"
            : sortBy?.ToLowerInvariant();

        // "relevance" is only meaningful with a term to rank against; otherwise fall through
        // to the date default rather than failing the request.
        if (effective == "relevance" && hasSearch)
        {
            var ranked = descending
                ? q.OrderByDescending(b => b.SearchVector.Rank(EF.Functions.WebSearchToTsQuery("english", searchTerm!)))
                : q.OrderBy(b => b.SearchVector.Rank(EF.Functions.WebSearchToTsQuery("english", searchTerm!)));

            // Identically-shaped matches score identically, so ties are the norm, not the
            // exception. Postgres may return tied rows in any order, which would let paging
            // repeat or skip rows between requests; Id makes the order total.
            return ranked.ThenByDescending(b => b.DateAdded).ThenBy(b => b.Id);
        }

        return effective switch
        {
            "title" => descending ? q.OrderByDescending(b => b.Title) : q.OrderBy(b => b.Title),
            "url" => descending ? q.OrderByDescending(b => b.Url) : q.OrderBy(b => b.Url),
            "datemodified" => descending ? q.OrderByDescending(b => b.DateModified) : q.OrderBy(b => b.DateModified),
            _ => descending ? q.OrderByDescending(b => b.DateAdded) : q.OrderBy(b => b.DateAdded)
        };
    }

    private static async Task<List<Tag>> ResolveTagsAsync(BookmarkDbContext context, IEnumerable<string> names, CancellationToken ct)
    {
        var normalized = names
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => (Raw: n.Trim(), Norm: n.Trim().ToLowerInvariant()))
            .GroupBy(x => x.Norm)
            .Select(g => g.First())
            .ToList();

        if (normalized.Count == 0)
        {
            return [];
        }

        var norms = normalized.Select(x => x.Norm).ToList();
        var existing = await context.Tags.Where(t => norms.Contains(t.NormalizedName)).ToListAsync(ct);
        var result = new List<Tag>(existing);

        foreach (var (raw, norm) in normalized)
        {
            if (existing.Any(t => t.NormalizedName == norm))
            {
                continue;
            }

            var tag = new Tag
            {
                Id = Guid.NewGuid(),
                Name = raw,
                NormalizedName = norm,
                DateCreated = DateTime.UtcNow
            };
            context.Tags.Add(tag);
            result.Add(tag);
        }

        return result;
    }

    private static IEnumerable<string> SplitCsv(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static BookmarkDto ToDto(Bookmark b)
    {
        var tags = b.BookmarkTags
            .Select(bt => new TagRefDto(bt.Tag.Id, bt.Tag.Name, bt.Tag.NormalizedName, bt.Tag.Color))
            .ToList();

        var categories = b.Category is null
            ? new List<CategoryRefDto>()
            : [new CategoryRefDto(b.Category.Id, b.Category.Name)];

        return new BookmarkDto(
            b.Id, b.Url, b.Title, b.Description, b.FaviconUrl, b.SourceFolder, b.IsRead,
            b.DateAdded, b.DateModified, tags, categories);
    }
}

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
    Task<BookmarkListResult> GetBookmarksAsync(BookmarkQuery query, CancellationToken ct = default);
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

public class BookmarkService(
    IDbContextFactory<BookmarkDbContext> contextFactory,
    IFaviconQueue faviconQueue) : IBookmarkService
{
    public async Task<BookmarkListResult> GetBookmarksAsync(BookmarkQuery query, CancellationToken ct = default)
    {
        // Capped so (page - 1) * pageSize cannot overflow into a negative OFFSET (a Postgres error,
        // i.e. a 500); a page past the end simply comes back empty.
        var page = Math.Clamp(query.Page is > 0 ? query.Page.Value : 1, 1, 1_000_000);
        var pageSize = Math.Clamp(query.PageSize ?? 20, 1, 100);

        await using var context = await contextFactory.CreateDbContextAsync(ct);

        var searchTerm = query.Search?.Trim();

        // The filtered set, without Includes: the count and the facet aggregates project their
        // own shapes and would only pay for the joins.
        var filtered = ApplyFilters(context, context.Bookmarks.AsNoTracking(), query, searchTerm);

        var total = await filtered.CountAsync(ct);

        // Facets describe the whole match, so they are computed before Skip/Take — a facet that
        // only counted the current page would be worse than none.
        var facets = HasNarrowingFilters(query) ? await BuildFacetsAsync(filtered, ct) : null;

        var paged = filtered
            .Include(b => b.Category)
            .Include(b => b.BookmarkTags)
                .ThenInclude(bt => bt.Tag);

        var items = await ApplySort(paged, query.SortBy, query.SortOrder, searchTerm)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var data = items.Select(ToDto).ToList();
        var totalPages = pageSize <= 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);
        return new BookmarkListResult(data, new PaginationDto(page, pageSize, total, totalPages), facets);
    }

    /// <summary>
    /// Whether anything is narrowing the result set. Paging and sorting don't count: they change
    /// what you see, not which bookmarks match.
    /// </summary>
    private static bool HasNarrowingFilters(BookmarkQuery query) =>
        !string.IsNullOrWhiteSpace(query.Search) ||
        !string.IsNullOrWhiteSpace(query.Tags) ||
        !string.IsNullOrWhiteSpace(query.Categories) ||
        !string.IsNullOrWhiteSpace(query.SourceFolder) ||
        query.IsRead.HasValue ||
        query.DateFrom.HasValue ||
        query.DateTo.HasValue;

    private static async Task<BookmarkFacetsDto> BuildFacetsAsync(
        IQueryable<Bookmark> filtered, CancellationToken ct)
    {
        // Ordered by count then name so the panel is stable between identical requests.
        // The ordering sits on the grouping, not the projection: EF cannot translate an
        // OrderBy over a property of an already-projected record.
        var tags = await filtered
            .SelectMany(b => b.BookmarkTags)
            .GroupBy(bt => new { bt.Tag.Id, bt.Tag.Name })
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key.Name)
            .Select(g => new FacetItemDto(g.Key.Id, g.Key.Name, g.Count()))
            .ToListAsync(ct);

        var categories = await filtered
            .Where(b => b.CategoryId != null)
            .GroupBy(b => new { Id = b.CategoryId!.Value, b.Category!.Name })
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key.Name)
            .Select(g => new FacetItemDto(g.Key.Id, g.Key.Name, g.Count()))
            .ToListAsync(ct);

        return new BookmarkFacetsDto(tags, categories);
    }

    public async Task<BookmarkDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(ct);
        var bookmark = await LoadWithGraph(context.Bookmarks.AsNoTracking())
            .FirstOrDefaultAsync(b => b.Id == id, ct);
        return bookmark is null ? null : ToDto(bookmark);
    }

    public Task<(BookmarkDto? Dto, BookmarkError Error)> CreateAsync(CreateBookmarkRequest request, CancellationToken ct = default) =>
        // A concurrent create of the same URL or a new tag loses at the unique index; the rerun's
        // pre-check then reports Duplicate instead of a 500.
        UniqueViolation.RetryLostRaceAsync(() => CreateCoreAsync(request, ct));

    private async Task<(BookmarkDto? Dto, BookmarkError Error)> CreateCoreAsync(CreateBookmarkRequest request, CancellationToken ct)
    {
        await using var context = await contextFactory.CreateDbContextAsync(ct);

        var url = request.Url.Trim();
        // Only SoftDelete is dropped: the unique index still covers soft-deleted rows, so a
        // resurrected URL must be caught. The Owner filter STAYS - without it this would report a
        // duplicate because somebody else saved that URL, leaking that they did.
        var exists = await context.Bookmarks.IgnoreQueryFilters(["SoftDelete"])
            .AnyAsync(b => b.Url == url, ct);
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

        // After the save, so the worker can never race the row it is about to load. Non-blocking
        // and ignorable: a dropped id keeps FaviconFetchedAt null and the startup backfill
        // retries it. Favicon enrichment must not be able to fail creating a bookmark.
        faviconQueue.TryEnqueue(bookmark.Id);

        return ((await GetByIdAsync(bookmark.Id, ct))!, BookmarkError.None);
    }

    public Task<(BookmarkDto? Dto, BookmarkError Error)> UpdateAsync(Guid id, UpdateBookmarkRequest request, CancellationToken ct = default) =>
        // New tag names race the same way as on create.
        UniqueViolation.RetryLostRaceAsync(() => UpdateCoreAsync(id, request, ct));

    private async Task<(BookmarkDto? Dto, BookmarkError Error)> UpdateCoreAsync(Guid id, UpdateBookmarkRequest request, CancellationToken ct)
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
        if (request.Description is not null)
        {
            bookmark.Description = request.Description.Length == 0 ? null : request.Description;
        }
        if (request.FaviconUrl is not null) bookmark.FaviconUrl = request.FaviconUrl;
        if (request.IsRead.HasValue) bookmark.IsRead = request.IsRead.Value;
        if (request.ClearCategory == true) bookmark.CategoryId = null;
        else if (request.CategoryId.HasValue) bookmark.CategoryId = request.CategoryId;

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
        await using var context = await contextFactory.CreateDbContextAsync(ct);

        BatchResultDto result;
        if (!context.Database.IsNpgsql())
        {
            // The in-memory test provider has no transactions or locks.
            result = await CreateBatchCoreAsync(context, request, ct);
        }
        else
        {
            // Two browsers syncing overlapping folders at once both pass the duplicate pre-check,
            // then deadlock or collide at the unique index - and the loser's WHOLE batch used to
            // fail with a 500. One account's syncs therefore take turns: a transaction-scoped
            // advisory lock keyed on the owner. Other accounts never touch the same unique index
            // entries, so they still run in parallel. The execution strategy owns the transaction
            // (required with Npgsql's retrying strategy) and reruns the whole unit on a transient
            // failure, which is why the change tracker is cleared at the top.
            var strategy = context.Database.CreateExecutionStrategy();
            result = await strategy.ExecuteAsync(async () =>
            {
                context.ChangeTracker.Clear();
                await using var transaction = await context.Database.BeginTransactionAsync(ct);
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT pg_advisory_xact_lock(hashtextextended({"batch:" + context.OwnerId}, 0))", ct);
                var inner = await CreateBatchCoreAsync(context, request, ct);
                await transaction.CommitAsync(ct);
                return inner;
            });
        }

        // After the commit, so the worker can never race rows it cannot see yet. Only what was
        // actually created - a skipped duplicate has already been enriched or recorded as
        // unenrichable. A 260-bookmark sync enqueues 260 ids and returns immediately; the worker
        // drains them slowly in the background.
        foreach (var item in result.Created)
        {
            faviconQueue.TryEnqueue(item.Id);
        }

        return result;
    }

    private static async Task<BatchResultDto> CreateBatchCoreAsync(
        BookmarkDbContext context, BatchCreateRequest request, CancellationToken ct)
    {
        var created = new List<BatchCreatedItem>();
        var skipped = new List<BatchSkippedItem>();
        var errors = new List<BatchErrorItem>();

        var items = request.Bookmarks ?? [];

        // Pre-load URLs that already exist (ignoring the soft-delete filter, because the unique
        // index still covers soft-deleted rows) to detect duplicates in a single round-trip.
        // Named filter, so ownership survives: dropping ALL filters here would make a new user's
        // first sync skip almost everything as "already present" because somebody else had it.
        var incomingUrls = items
            .Select(i => i.Url?.Trim())
            .Where(u => !string.IsNullOrEmpty(u))
            .Select(u => u!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var existingUrls = incomingUrls.Count == 0
            ? []
            : await context.Bookmarks.IgnoreQueryFilters(["SoftDelete"])
                .Where(b => incomingUrls.Contains(b.Url))
                .Select(b => b.Url)
                .ToListAsync(ct);

        // Ordinal, like the unique index and the single-create check: URL paths and queries are
        // case-sensitive (youtube.com/watch?v=abcD and ?v=abcd are different videos).
        var known = new HashSet<string>(existingUrls, StringComparer.Ordinal);
        var seenInBatch = new HashSet<string>(StringComparer.Ordinal);

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

            var dateAdded = FromUnixMilliseconds(item.DateAdded) ?? DateTime.UtcNow;

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

    /// <summary>Makes % and _ in a search term literal, so "100%" does not match every tag.</summary>
    private static string EscapeLike(string term) =>
        term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    /// <summary>
    /// A browser's "date added", or null when it is absent or not a plausible timestamp. One
    /// corrupt value (microseconds, say, which overflows DateTime) used to throw and fail the whole
    /// sync; such a bookmark is now simply dated "now", like one without a date.
    /// </summary>
    internal static DateTime? FromUnixMilliseconds(long? milliseconds)
    {
        // 1990-01-01 .. 2100-01-01: wider than any real bookmark, far inside DateTime's range.
        const long earliest = 631_152_000_000;
        const long latest = 4_102_444_800_000;
        return milliseconds is >= earliest and <= latest
            ? DateTime.UnixEpoch.AddMilliseconds(milliseconds.Value)
            : null;
    }

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

            var tagPattern = $"%{EscapeLike(searchTerm)}%";
            var tagMatches = context.Bookmarks
                .Where(b => b.BookmarkTags.Any(bt => EF.Functions.ILike(bt.Tag.Name, tagPattern, "\\")))
                .Select(b => b.Id);

            var matchingIds = textMatches.Union(tagMatches);
            q = q.Where(b => matchingIds.Contains(b.Id));
        }

        if (!string.IsNullOrWhiteSpace(query.Tags))
        {
            var tagNames = SplitTagList(query.Tags).Select(t => t.ToLowerInvariant()).ToList();
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

        IOrderedQueryable<Bookmark> ordered = effective switch
        {
            "title" => descending ? q.OrderByDescending(b => b.Title) : q.OrderBy(b => b.Title),
            "url" => descending ? q.OrderByDescending(b => b.Url) : q.OrderBy(b => b.Url),
            "datemodified" => descending ? q.OrderByDescending(b => b.DateModified) : q.OrderBy(b => b.DateModified),
            _ => descending ? q.OrderByDescending(b => b.DateAdded) : q.OrderBy(b => b.DateAdded)
        };

        // Same reason as above: an import gives many bookmarks the same title or timestamp, and
        // without a total order those rows can repeat or vanish between pages.
        return ordered.ThenBy(b => b.Id);
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

    /// <summary>
    /// Splits the <c>tags</c> filter. Tag names may contain commas (nothing forbids them over the
    /// API), so <c>\,</c> is a literal comma and <c>\\</c> a literal backslash - the web app encodes
    /// the same way. Plain comma-separated lists, i.e. every existing URL, parse as before.
    /// </summary>
    private static List<string> SplitTagList(string value)
    {
        var names = new List<string>();
        var current = new System.Text.StringBuilder();
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (ch == '\\' && i + 1 < value.Length) current.Append(value[++i]);
            else if (ch == ',') { names.Add(current.ToString()); current.Clear(); }
            else current.Append(ch);
        }
        names.Add(current.ToString());
        return names.Select(n => n.Trim()).Where(n => n.Length > 0).ToList();
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

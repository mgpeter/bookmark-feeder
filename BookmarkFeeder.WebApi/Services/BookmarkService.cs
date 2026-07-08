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

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            q = q.Where(b =>
                EF.Functions.ILike(b.Title, pattern) ||
                EF.Functions.ILike(b.Url, pattern) ||
                (b.Description != null && EF.Functions.ILike(b.Description, pattern)));
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

        var total = await q.CountAsync(ct);

        q = ApplySort(q, query.SortBy, query.SortOrder);

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

    private static IQueryable<Bookmark> ApplySort(IQueryable<Bookmark> q, string? sortBy, string? sortOrder)
    {
        var descending = !string.Equals(sortOrder, "asc", StringComparison.OrdinalIgnoreCase);
        return (sortBy?.ToLowerInvariant()) switch
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

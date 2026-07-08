using BookmarkFeeder.WebApi.Data;
using BookmarkFeeder.WebApi.Dtos;
using BookmarkFeeder.WebApi.Models;
using Microsoft.EntityFrameworkCore;

namespace BookmarkFeeder.WebApi.Services;

public enum CategoryError
{
    None,
    NotFound,
    InvalidParent,
    CircularReference,
    InvalidReassign
}

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> GetTreeAsync(CancellationToken ct = default);
    Task<(CategoryDto? Dto, CategoryError Error)> CreateAsync(CreateCategoryRequest request, CancellationToken ct = default);
    Task<(CategoryDto? Dto, CategoryError Error)> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken ct = default);
    Task<CategoryError> DeleteAsync(Guid id, Guid? reassignTo, CancellationToken ct = default);
}

public class CategoryService(IDbContextFactory<BookmarkDbContext> contextFactory) : ICategoryService
{
    public async Task<IReadOnlyList<CategoryDto>> GetTreeAsync(CancellationToken ct = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(ct);

        var categories = await context.Categories.AsNoTracking().ToListAsync(ct);
        var counts = await context.Bookmarks
            .Where(b => b.CategoryId != null)
            .GroupBy(b => b.CategoryId!.Value)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Count, ct);

        var byParent = categories
            .GroupBy(c => c.ParentCategoryId)
            .ToDictionary(g => g.Key ?? Guid.Empty, g => g.OrderBy(c => c.Name).ToList());

        List<CategoryDto> Build(Guid? parentId, int level)
        {
            var key = parentId ?? Guid.Empty;
            if (!byParent.TryGetValue(key, out var children))
            {
                return [];
            }

            return children
                .Select(c => new CategoryDto(
                    c.Id, c.Name, c.Description, c.ParentCategoryId, level,
                    counts.GetValueOrDefault(c.Id), c.DateCreated,
                    Build(c.Id, level + 1)))
                .ToList();
        }

        return Build(null, 0);
    }

    public async Task<(CategoryDto? Dto, CategoryError Error)> CreateAsync(CreateCategoryRequest request, CancellationToken ct = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(ct);

        if (request.ParentCategoryId is { } parentId && !await context.Categories.AnyAsync(c => c.Id == parentId, ct))
        {
            return (null, CategoryError.InvalidParent);
        }

        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = request.Description,
            ParentCategoryId = request.ParentCategoryId,
            DateCreated = DateTime.UtcNow
        };

        context.Categories.Add(category);
        await context.SaveChangesAsync(ct);

        var level = await ComputeLevelAsync(context, category.ParentCategoryId, ct);
        return (ToDto(category, level, 0), CategoryError.None);
    }

    public async Task<(CategoryDto? Dto, CategoryError Error)> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken ct = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(ct);
        var category = await context.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (category is null)
        {
            return (null, CategoryError.NotFound);
        }

        if (request.ParentCategoryId is { } parentId)
        {
            if (parentId == id || !await context.Categories.AnyAsync(c => c.Id == parentId, ct))
            {
                return (null, parentId == id ? CategoryError.CircularReference : CategoryError.InvalidParent);
            }

            if (await WouldCreateCycleAsync(context, id, parentId, ct))
            {
                return (null, CategoryError.CircularReference);
            }
        }

        category.Name = request.Name.Trim();
        category.Description = request.Description;
        category.ParentCategoryId = request.ParentCategoryId;
        await context.SaveChangesAsync(ct);

        var level = await ComputeLevelAsync(context, category.ParentCategoryId, ct);
        var count = await context.Bookmarks.CountAsync(b => b.CategoryId == id, ct);
        return (ToDto(category, level, count), CategoryError.None);
    }

    public async Task<CategoryError> DeleteAsync(Guid id, Guid? reassignTo, CancellationToken ct = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(ct);
        var category = await context.Categories
            .Include(c => c.SubCategories)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        if (category is null)
        {
            return CategoryError.NotFound;
        }

        if (reassignTo is { } target)
        {
            if (target == id || !await context.Categories.AnyAsync(c => c.Id == target, ct))
            {
                return CategoryError.InvalidReassign;
            }
        }

        // Reassign bookmarks to the target category (or detach them when no target given).
        var bookmarks = await context.Bookmarks.IgnoreQueryFilters()
            .Where(b => b.CategoryId == id)
            .ToListAsync(ct);
        foreach (var bookmark in bookmarks)
        {
            bookmark.CategoryId = reassignTo;
        }

        // Promote children to the reassign target, or up to the deleted node's parent.
        var newParent = reassignTo ?? category.ParentCategoryId;
        foreach (var child in category.SubCategories)
        {
            child.ParentCategoryId = newParent;
        }

        context.Categories.Remove(category);
        await context.SaveChangesAsync(ct);
        return CategoryError.None;
    }

    // ----- helpers -----

    private static async Task<int> ComputeLevelAsync(BookmarkDbContext context, Guid? parentId, CancellationToken ct)
    {
        var level = 0;
        var current = parentId;
        while (current is { } id)
        {
            level++;
            current = await context.Categories
                .Where(c => c.Id == id)
                .Select(c => c.ParentCategoryId)
                .FirstOrDefaultAsync(ct);
            if (level > 100) break; // safety against malformed data
        }
        return level;
    }

    private static async Task<bool> WouldCreateCycleAsync(BookmarkDbContext context, Guid categoryId, Guid newParentId, CancellationToken ct)
    {
        var current = (Guid?)newParentId;
        var guard = 0;
        while (current is { } id)
        {
            if (id == categoryId)
            {
                return true;
            }
            current = await context.Categories
                .Where(c => c.Id == id)
                .Select(c => c.ParentCategoryId)
                .FirstOrDefaultAsync(ct);
            if (++guard > 100) break;
        }
        return false;
    }

    private static CategoryDto ToDto(Category c, int level, int bookmarkCount) =>
        new(c.Id, c.Name, c.Description, c.ParentCategoryId, level, bookmarkCount, c.DateCreated, []);
}

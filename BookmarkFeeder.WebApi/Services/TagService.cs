using BookmarkFeeder.WebApi.Data;
using BookmarkFeeder.WebApi.Dtos;
using BookmarkFeeder.WebApi.Models;
using Microsoft.EntityFrameworkCore;

namespace BookmarkFeeder.WebApi.Services;

public enum TagError
{
    None,
    NotFound,
    Duplicate
}

public interface ITagService
{
    Task<IReadOnlyList<TagDto>> GetAllAsync(CancellationToken ct = default);
    Task<(TagDto? Dto, TagError Error)> CreateAsync(CreateTagRequest request, CancellationToken ct = default);
    Task<(TagDto? Dto, TagError Error)> UpdateAsync(Guid id, UpdateTagRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}

public class TagService(IDbContextFactory<BookmarkDbContext> contextFactory) : ITagService
{
    public async Task<IReadOnlyList<TagDto>> GetAllAsync(CancellationToken ct = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(ct);
        return await context.Tags
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new TagDto(
                t.Id, t.Name, t.NormalizedName, t.Color, t.DateCreated,
                t.BookmarkTags.Count(bt => !bt.Bookmark.IsDeleted)))
            .ToListAsync(ct);
    }

    public async Task<(TagDto? Dto, TagError Error)> CreateAsync(CreateTagRequest request, CancellationToken ct = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(ct);

        var normalized = request.Name.Trim().ToLowerInvariant();
        if (await context.Tags.AnyAsync(t => t.NormalizedName == normalized, ct))
        {
            return (null, TagError.Duplicate);
        }

        var tag = new Tag
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            NormalizedName = normalized,
            Color = request.Color,
            DateCreated = DateTime.UtcNow
        };

        context.Tags.Add(tag);
        await context.SaveChangesAsync(ct);

        return (new TagDto(tag.Id, tag.Name, tag.NormalizedName, tag.Color, tag.DateCreated, 0), TagError.None);
    }

    public async Task<(TagDto? Dto, TagError Error)> UpdateAsync(Guid id, UpdateTagRequest request, CancellationToken ct = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(ct);
        var tag = await context.Tags.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tag is null)
        {
            return (null, TagError.NotFound);
        }

        var normalized = request.Name.Trim().ToLowerInvariant();
        if (normalized != tag.NormalizedName &&
            await context.Tags.AnyAsync(t => t.NormalizedName == normalized && t.Id != id, ct))
        {
            return (null, TagError.Duplicate);
        }

        tag.Name = request.Name.Trim();
        tag.NormalizedName = normalized;
        tag.Color = request.Color;
        await context.SaveChangesAsync(ct);

        var count = await context.BookmarkTags.CountAsync(bt => bt.TagId == id && !bt.Bookmark.IsDeleted, ct);
        return (new TagDto(tag.Id, tag.Name, tag.NormalizedName, tag.Color, tag.DateCreated, count), TagError.None);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(ct);
        var tag = await context.Tags.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tag is null)
        {
            return false;
        }

        context.Tags.Remove(tag);
        await context.SaveChangesAsync(ct);
        return true;
    }
}

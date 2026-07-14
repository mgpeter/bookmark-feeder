using BookmarkFeeder.WebApi.Data;
using BookmarkFeeder.WebApi.Dtos;
using BookmarkFeeder.WebApi.Models;
using Microsoft.EntityFrameworkCore;

namespace BookmarkFeeder.WebApi.Services;

public interface ISavedSearchService
{
    Task<IReadOnlyList<SavedSearchDto>> GetAllAsync(CancellationToken ct = default);
    Task<SavedSearchDto> CreateAsync(CreateSavedSearchRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}

public class SavedSearchService(IDbContextFactory<BookmarkDbContext> contextFactory) : ISavedSearchService
{
    public async Task<IReadOnlyList<SavedSearchDto>> GetAllAsync(CancellationToken ct = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(ct);

        // Newest first: the most recently saved search is the one most likely wanted again.
        return await context.SavedSearches
            .AsNoTracking()
            .OrderByDescending(s => s.DateCreated)
            .Select(s => new SavedSearchDto(s.Id, s.Name, s.Query, s.DateCreated))
            .ToListAsync(ct);
    }

    public async Task<SavedSearchDto> CreateAsync(
        CreateSavedSearchRequest request, CancellationToken ct = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(ct);

        var saved = new SavedSearch
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Query = request.Query.Trim(),
            DateCreated = DateTime.UtcNow,
        };

        context.SavedSearches.Add(saved);
        await context.SaveChangesAsync(ct);

        return new SavedSearchDto(saved.Id, saved.Name, saved.Query, saved.DateCreated);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(ct);

        // Hard delete: a saved search is a shortcut, not a record worth keeping.
        var deleted = await context.SavedSearches.Where(s => s.Id == id).ExecuteDeleteAsync(ct);
        return deleted > 0;
    }
}

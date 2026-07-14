using BookmarkFeeder.WebApi.Dtos;
using BookmarkFeeder.WebApi.Filters;
using BookmarkFeeder.WebApi.Services;

namespace BookmarkFeeder.WebApi.Endpoints;

public static class BookmarkEndpoints
{
    public static RouteGroupBuilder MapBookmarkEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (IBookmarkService service, [AsParameters] BookmarkQuery query, CancellationToken ct) =>
                TypedResults.Ok(await service.GetBookmarksAsync(query, ct)))
            .WithName("GetBookmarks");

        group.MapGet("/{id:guid}", async (Guid id, IBookmarkService service, CancellationToken ct) =>
            {
                var bookmark = await service.GetByIdAsync(id, ct);
                return bookmark is null ? Results.NotFound() : Results.Ok(bookmark);
            })
            .WithName("GetBookmarkById");

        group.MapPost("/", async (CreateBookmarkRequest request, IBookmarkService service, CancellationToken ct) =>
            {
                var (dto, error) = await service.CreateAsync(request, ct);
                return error switch
                {
                    BookmarkError.Duplicate => Results.Conflict(new { message = "A bookmark with this URL already exists." }),
                    BookmarkError.InvalidCategory => Results.BadRequest(new { message = "The specified category does not exist." }),
                    _ => Results.Created($"/api/bookmarks/{dto!.Id}", dto)
                };
            })
            .AddEndpointFilter<ValidationFilter<CreateBookmarkRequest>>()
            .WithName("CreateBookmark")
            .RequireRateLimiting("writes");

        group.MapPost("/batch", async (BatchCreateRequest request, IBookmarkService service, CancellationToken ct) =>
                TypedResults.Ok(await service.CreateBatchAsync(request, ct)))
            .AddEndpointFilter<ValidationFilter<BatchCreateRequest>>()
            .WithName("CreateBookmarksBatch")
            .RequireRateLimiting("sync");

        group.MapPut("/{id:guid}", async (Guid id, UpdateBookmarkRequest request, IBookmarkService service, CancellationToken ct) =>
            {
                var (dto, error) = await service.UpdateAsync(id, request, ct);
                if (error == BookmarkError.InvalidCategory)
                {
                    return Results.BadRequest(new { message = "The specified category does not exist." });
                }
                return dto is null ? Results.NotFound() : Results.Ok(dto);
            })
            .AddEndpointFilter<ValidationFilter<UpdateBookmarkRequest>>()
            .WithName("UpdateBookmark")
            .RequireRateLimiting("writes");

        group.MapDelete("/{id:guid}", async (Guid id, IBookmarkService service, CancellationToken ct) =>
            {
                var deleted = await service.DeleteAsync(id, ct);
                return deleted ? Results.NoContent() : Results.NotFound();
            })
            .WithName("DeleteBookmark")
            .RequireRateLimiting("writes");

        group.MapPatch("/{id:guid}/read", async (Guid id, MarkReadRequest request, IBookmarkService service, CancellationToken ct) =>
            {
                var dto = await service.SetReadAsync(id, request.IsRead, ct);
                return dto is null ? Results.NotFound() : Results.Ok(dto);
            })
            .WithName("MarkBookmarkRead")
            .RequireRateLimiting("writes");

        // Filters bind from the query string — the same BookmarkQuery the GET binds, so the client
        // sends one filter string for both and the marked set cannot drift from the shown set.
        // The target state binds from the body: 'isRead' in the query string is already the
        // read-state *filter*, so ?isRead=false + {"isRead":true} means "mark the unread ones read".
        group.MapPost("/mark-read", async (
                [AsParameters] BookmarkQuery query,
                MarkReadRequest request,
                IBookmarkService service,
                CancellationToken ct) =>
                TypedResults.Ok(new { updated = await service.MarkAllReadAsync(query, request.IsRead, ct) }))
            .WithName("MarkAllBookmarksRead")
            .RequireRateLimiting("writes");

        return group;
    }
}

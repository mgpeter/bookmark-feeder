using BookmarkFeeder.WebApi.Dtos;
using BookmarkFeeder.WebApi.Filters;
using BookmarkFeeder.WebApi.Services;

namespace BookmarkFeeder.WebApi.Endpoints;

public static class SavedSearchEndpoints
{
    public static RouteGroupBuilder MapSavedSearchEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (ISavedSearchService service, CancellationToken ct) =>
                TypedResults.Ok(await service.GetAllAsync(ct)))
            .WithName("GetSavedSearches");

        group.MapPost("/", async (CreateSavedSearchRequest request, ISavedSearchService service, CancellationToken ct) =>
            {
                var dto = await service.CreateAsync(request, ct);
                return Results.Created($"/api/searches/{dto.Id}", dto);
            })
            .AddEndpointFilter<ValidationFilter<CreateSavedSearchRequest>>()
            .WithName("CreateSavedSearch")
            .RequireRateLimiting("writes");

        group.MapDelete("/{id:guid}", async (Guid id, ISavedSearchService service, CancellationToken ct) =>
            {
                var deleted = await service.DeleteAsync(id, ct);
                return deleted ? Results.NoContent() : Results.NotFound();
            })
            .WithName("DeleteSavedSearch")
            .RequireRateLimiting("writes");

        return group;
    }
}

using BookmarkFeeder.WebApi.Dtos;
using BookmarkFeeder.WebApi.Filters;
using BookmarkFeeder.WebApi.Services;

namespace BookmarkFeeder.WebApi.Endpoints;

public static class TagEndpoints
{
    public static RouteGroupBuilder MapTagEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (ITagService service, CancellationToken ct) =>
                TypedResults.Ok(await service.GetAllAsync(ct)))
            .WithName("GetTags");

        group.MapPost("/", async (CreateTagRequest request, ITagService service, CancellationToken ct) =>
            {
                var (dto, error) = await service.CreateAsync(request, ct);
                return error == TagError.Duplicate
                    ? Results.Conflict(new { message = "A tag with this name already exists." })
                    : Results.Created($"/api/tags/{dto!.Id}", dto);
            })
            .AddEndpointFilter<ValidationFilter<CreateTagRequest>>()
            .WithName("CreateTag");

        group.MapPut("/{id:guid}", async (Guid id, UpdateTagRequest request, ITagService service, CancellationToken ct) =>
            {
                var (dto, error) = await service.UpdateAsync(id, request, ct);
                return error switch
                {
                    TagError.NotFound => Results.NotFound(),
                    TagError.Duplicate => Results.Conflict(new { message = "A tag with this name already exists." }),
                    _ => Results.Ok(dto)
                };
            })
            .AddEndpointFilter<ValidationFilter<UpdateTagRequest>>()
            .WithName("UpdateTag");

        group.MapDelete("/{id:guid}", async (Guid id, ITagService service, CancellationToken ct) =>
            {
                var deleted = await service.DeleteAsync(id, ct);
                return deleted ? Results.NoContent() : Results.NotFound();
            })
            .WithName("DeleteTag");

        return group;
    }
}

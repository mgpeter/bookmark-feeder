using BookmarkFeeder.WebApi.Dtos;
using BookmarkFeeder.WebApi.Filters;
using BookmarkFeeder.WebApi.Services;

namespace BookmarkFeeder.WebApi.Endpoints;

public static class CategoryEndpoints
{
    public static RouteGroupBuilder MapCategoryEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (ICategoryService service, CancellationToken ct) =>
                TypedResults.Ok(await service.GetTreeAsync(ct)))
            .WithName("GetCategories");

        group.MapPost("/", async (CreateCategoryRequest request, ICategoryService service, CancellationToken ct) =>
            {
                var (dto, error) = await service.CreateAsync(request, ct);
                return error == CategoryError.InvalidParent
                    ? Results.BadRequest(new { message = "The specified parent category does not exist." })
                    : Results.Created($"/api/categories/{dto!.Id}", dto);
            })
            .AddEndpointFilter<ValidationFilter<CreateCategoryRequest>>()
            .WithName("CreateCategory");

        group.MapPut("/{id:guid}", async (Guid id, UpdateCategoryRequest request, ICategoryService service, CancellationToken ct) =>
            {
                var (dto, error) = await service.UpdateAsync(id, request, ct);
                return error switch
                {
                    CategoryError.NotFound => Results.NotFound(),
                    CategoryError.InvalidParent => Results.BadRequest(new { message = "The specified parent category does not exist." }),
                    CategoryError.CircularReference => Results.BadRequest(new { message = "A category cannot be its own ancestor." }),
                    _ => Results.Ok(dto)
                };
            })
            .AddEndpointFilter<ValidationFilter<UpdateCategoryRequest>>()
            .WithName("UpdateCategory");

        group.MapDelete("/{id:guid}", async (Guid id, Guid? reassignTo, ICategoryService service, CancellationToken ct) =>
            {
                var error = await service.DeleteAsync(id, reassignTo, ct);
                return error switch
                {
                    CategoryError.NotFound => Results.NotFound(),
                    CategoryError.InvalidReassign => Results.BadRequest(new { message = "The reassignment target category is invalid." }),
                    _ => Results.NoContent()
                };
            })
            .WithName("DeleteCategory");

        return group;
    }
}

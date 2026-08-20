using BookmarkFeeder.WebApi.Auth;
using BookmarkFeeder.WebApi.Dtos;
using BookmarkFeeder.WebApi.Filters;
using BookmarkFeeder.WebApi.Services;

namespace BookmarkFeeder.WebApi.Endpoints;

/// <summary>
/// Manages the keys the browser extension authenticates with.
///
/// Note there is no [AllowApiKey] anywhere here: a key can never manage keys. That falls out of
/// ApiKeyScopeFilter's default-deny rather than needing a rule of its own.
/// </summary>
public static class ApiKeyEndpoints
{
    public static RouteGroupBuilder MapApiKeyEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (ICurrentUserContext user, IApiKeyService service, CancellationToken ct) =>
                TypedResults.Ok(await service.GetAllAsync(user.UserId, ct)))
            .WithName("GetApiKeys");

        group.MapPost("/", async (
                CreateApiKeyRequest request,
                ICurrentUserContext user,
                IApiKeyService service,
                CancellationToken ct) =>
            {
                var dto = await service.CreateAsync(user.UserId, request, ct);
                // The response body carries the plaintext key. It is the only time it exists
                // outside the caller's browser, hence the explicit 201 with the full DTO.
                return Results.Created($"/api/keys/{dto.Id}", dto);
            })
            .AddEndpointFilter<ValidationFilter<CreateApiKeyRequest>>()
            .WithName("CreateApiKey")
            .RequireRateLimiting("writes");

        group.MapDelete("/{id:guid}", async (
                Guid id,
                ICurrentUserContext user,
                IApiKeyService service,
                CancellationToken ct) =>
            {
                var revoked = await service.RevokeAsync(user.UserId, id, ct);
                return revoked ? Results.NoContent() : Results.NotFound();
            })
            .WithName("RevokeApiKey")
            .RequireRateLimiting("writes");

        return group;
    }
}

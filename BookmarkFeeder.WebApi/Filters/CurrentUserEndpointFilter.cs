using System.Security.Claims;
using BookmarkFeeder.WebApi.Auth;

namespace BookmarkFeeder.WebApi.Filters;

/// <summary>
/// Publishes the caller's user id on the scoped ICurrentUserContext, which is what the "Owner"
/// global query filter in BookmarkDbContext reads.
///
/// An endpoint filter rather than middleware: it is scoped to the /api group (so /health and the
/// OpenAPI endpoints stay untouched), it is guaranteed to run only after RequireAuthorization has
/// already succeeded, and it resolves from the request scope. It occupies exactly the slot the old
/// ApiKeyEndpointFilter did.
/// </summary>
public sealed class CurrentUserEndpointFilter(
    IUserProvisioningService provisioning,
    ICurrentUserContext currentUser) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;

        // CORS preflight carries no credentials; let it through as the old filter did.
        if (HttpMethods.IsOptions(httpContext.Request.Method))
        {
            return await next(context);
        }

        var principal = httpContext.User;

        // An API key already resolved its owner during authentication, so there is nothing to look
        // up and nothing to provision - a key can only exist for a user who has already signed in.
        var ownerClaim = principal.FindFirstValue(ApiKeyAuthenticationHandler.OwnerIdClaim);
        if (ownerClaim is not null && Guid.TryParse(ownerClaim, out var ownerId))
        {
            currentUser.Set(ownerId);
            return await next(context);
        }

        currentUser.Set(await provisioning.ResolveAsync(principal, httpContext.RequestAborted));
        return await next(context);
    }
}

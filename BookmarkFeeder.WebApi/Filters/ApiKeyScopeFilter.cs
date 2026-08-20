using System.Security.Claims;
using BookmarkFeeder.WebApi.Auth;

namespace BookmarkFeeder.WebApi.Filters;

/// <summary>
/// Marks an endpoint as reachable with a minted API key. Absence is denial.
/// </summary>
public sealed class AllowApiKeyAttribute : Attribute;

/// <summary>
/// Confines API keys to an explicit allow-list.
///
/// FAIL CLOSED on purpose: an endpoint added later is automatically unreachable with a key, so
/// nobody has to remember to lock it down. A key leaked from a browser profile can add bookmarks
/// and read tags; it cannot delete a collection or mint more keys.
/// </summary>
public sealed class ApiKeyScopeFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;

        if (HttpMethods.IsOptions(httpContext.Request.Method))
        {
            return await next(context);
        }

        // Only key-authenticated principals are restricted; a signed-in human is unaffected.
        var isApiKey = httpContext.User.FindFirstValue(ApiKeyAuthenticationHandler.KeyIdClaim) is not null;
        if (!isApiKey)
        {
            return await next(context);
        }

        var allowed = httpContext.GetEndpoint()?.Metadata.GetMetadata<AllowApiKeyAttribute>() is not null;
        if (!allowed)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "This API key is limited to syncing.",
                detail: "API keys may create bookmarks in bulk and read tags and categories. "
                      + "Sign in to the web app for anything else.");
        }

        return await next(context);
    }
}

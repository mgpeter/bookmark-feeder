using System.Security.Cryptography;
using System.Text;

namespace BookmarkFeeder.WebApi.Filters;

/// <summary>
/// Validates the <c>X-API-Key</c> header against the configured key. Applied to the whole
/// <c>/api</c> group so health, OpenAPI and Scalar endpoints stay open.
/// </summary>
public class ApiKeyEndpointFilter(IConfiguration configuration, ILogger<ApiKeyEndpointFilter> logger) : IEndpointFilter
{
    public const string HeaderName = "X-API-Key";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;

        // Let CORS preflight through untouched.
        if (HttpMethods.IsOptions(httpContext.Request.Method))
        {
            return await next(context);
        }

        var expected = configuration["Authentication:ApiKey"];
        if (string.IsNullOrEmpty(expected))
        {
            logger.LogError("No API key configured (Authentication:ApiKey). Rejecting request.");
            return TypedResults.Problem(statusCode: StatusCodes.Status500InternalServerError, title: "API key not configured.");
        }

        var provided = httpContext.Request.Headers[HeaderName].ToString();
        if (string.IsNullOrEmpty(provided) || !FixedTimeEquals(provided, expected))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid or missing API key.");
        }

        return await next(context);
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var ba = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        return ba.Length == bb.Length && CryptographicOperations.FixedTimeEquals(ba, bb);
    }
}

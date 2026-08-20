using System.Security.Claims;
using System.Text.Encodings.Web;
using BookmarkFeeder.WebApi.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace BookmarkFeeder.WebApi.Auth;

/// <summary>
/// Authenticates the browser extension with a user-minted API key.
///
/// Unlike the shared key this replaces, a key resolves to exactly one owner (enforced by a unique
/// index on the hash) and reaches only the endpoints ApiKeyScopeFilter allows.
/// </summary>
public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    IApiKeyService apiKeys)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-API-Key";

    /// <summary>Carries the resolved owner, so no provisioning lookup is needed for key requests.</summary>
    public const string OwnerIdClaim = "bmf:owner";

    /// <summary>Marks the principal as key-authenticated, which ApiKeyScopeFilter keys off.</summary>
    public const string KeyIdClaim = "bmf:key";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var header))
        {
            return AuthenticateResult.NoResult();
        }

        var presented = header.ToString();
        if (string.IsNullOrWhiteSpace(presented))
        {
            return AuthenticateResult.NoResult();
        }

        var result = await apiKeys.ValidateAsync(presented, Context.RequestAborted);
        if (result is null)
        {
            // One message for unknown, revoked and expired alike: the response must not confirm
            // that a given key ever existed.
            return AuthenticateResult.Fail("Invalid API key.");
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(OwnerIdClaim, result.OwnerId.ToString()),
                new Claim(KeyIdClaim, result.KeyId.ToString()),
            ],
            SchemeName);

        return AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.ContentType = "application/problem+json";
        Response.Headers.WWWAuthenticate = HeaderName;

        await Response.WriteAsJsonAsync(new
        {
            type = "https://tools.ietf.org/html/rfc9110#section-15.5.2",
            title = "Unauthorized",
            status = StatusCodes.Status401Unauthorized,
            detail = "The API key is missing, invalid, revoked or expired.",
        });
    }
}

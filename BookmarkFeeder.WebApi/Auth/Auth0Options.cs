namespace BookmarkFeeder.WebApi.Auth;

/// <summary>
/// Auth0 tenant coordinates. None of these are secrets: the domain, the API audience and the SPA
/// client id are all public in any browser bundle. They are configuration, not credentials.
/// </summary>
public sealed class Auth0Options
{
    public const string SectionName = "Auth0";

    /// <summary>Tenant domain WITHOUT a scheme, e.g. "usualexpat.uk.auth0.com".</summary>
    public string? Domain { get; set; }

    /// <summary>The Auth0 API identifier (audience), e.g. "https://api.bookmarkfeeder".</summary>
    public string? Audience { get; set; }

    /// <summary>Public SPA client id. Surfaced to the frontend; never used server-side to sign.</summary>
    public string? ClientId { get; set; }

    /// <summary>How long a resolved sub -> userId mapping is cached before re-checking the database.</summary>
    public int UserCacheMinutes { get; set; } = 30;

    /// <summary>Minimum interval between LastSeenAt / LastUsedAt writes for the same principal.</summary>
    public int LastSeenThrottleMinutes { get; set; } = 60;

    /// <summary>
    /// The OIDC issuer. Auth0 always issues with a scheme AND a trailing slash; omitting either
    /// makes every token fail issuer validation with a message that does not say why.
    /// </summary>
    public string Issuer => $"https://{Domain}/";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Domain) && !string.IsNullOrWhiteSpace(Audience);
}

using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace BookmarkFeeder.WebApi.Tests.Infrastructure;

/// <summary>
/// Mints tokens the application's real JwtBearer handler accepts.
///
/// The factories replace only the SIGNING KEY and the metadata lookup, so no test ever reaches
/// Auth0 over the network. Issuer, audience, lifetime, the "sub"/"email" claims, authorization and
/// just-in-time user provisioning are all the production code paths.
/// </summary>
public static class TestTokens
{
    public const string Issuer = "https://bookmarkfeeder-tests.local/";
    public const string Audience = "https://api.bookmarkfeeder.tests";

    /// <summary>HS256 rejects anything shorter than 32 bytes.</summary>
    private static readonly byte[] KeyBytes =
        "bookmarkfeeder-integration-test-signing-key-0001"u8.ToArray();

    public static SymmetricSecurityKey SigningKey => new(KeyBytes);

    public static string Create(
        string sub,
        string? email = null,
        string? name = null,
        DateTime? expires = null,
        string? issuer = null,
        string? audience = null,
        SecurityKey? signingKey = null)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer ?? Issuer,
            Audience = audience ?? Audience,
            Expires = expires ?? DateTime.UtcNow.AddMinutes(30),
            SigningCredentials = new SigningCredentials(
                signingKey ?? SigningKey, SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>
            {
                ["sub"] = sub,
                ["email"] = email ?? $"{sub.Replace('|', '-')}@example.test",
                ["name"] = name ?? sub,
            },
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}

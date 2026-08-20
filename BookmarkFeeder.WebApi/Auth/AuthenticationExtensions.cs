using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace BookmarkFeeder.WebApi.Auth;

public static class AuthenticationExtensions
{
    /// <summary>
    /// The raw OIDC subject claim. MapInboundClaims is disabled, so it keeps this name rather than
    /// being rewritten to ClaimTypes.NameIdentifier - both the rate limiter and the user resolver
    /// read "sub" directly.
    /// </summary>
    public const string SubClaim = "sub";

    /// <summary>
    /// Picks a scheme per request: the extension sends X-API-Key, the web app sends a bearer token.
    /// </summary>
    public const string SmartScheme = "Smart";

    public static IHostApplicationBuilder AddBookmarkFeederAuthentication(this IHostApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<Auth0Options>()
            .Bind(builder.Configuration.GetSection(Auth0Options.SectionName));

        var auth0 = builder.Configuration.GetSection(Auth0Options.SectionName).Get<Auth0Options>() ?? new Auth0Options();

        builder.Services.AddAuthentication(SmartScheme)
            .AddPolicyScheme(SmartScheme, SmartScheme, options =>
            {
                options.ForwardDefaultSelector = context =>
                    context.Request.Headers.ContainsKey(ApiKeyAuthenticationHandler.HeaderName)
                        ? ApiKeyAuthenticationHandler.SchemeName
                        : JwtBearerDefaults.AuthenticationScheme;
            })
            .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationHandler.SchemeName, _ => { })
            .AddJwtBearer(options =>
            {
                // Guarded: with an empty Domain this builds "https:///", which is not a valid Uri
                // and throws inside JwtBearerPostConfigureOptions. That would break
                // `dotnet ef migrations add` and the build-time GetDocument OpenAPI generation,
                // both of which run with no Auth0 configuration at all.
                if (auth0.IsConfigured)
                {
                    options.Authority = auth0.Issuer;   // -> discovery doc -> JWKS
                    options.Audience = auth0.Audience;
                }

                options.RequireHttpsMetadata = true;    // Auth0 is always https, even in dev
                options.MapInboundClaims = false;       // keep "sub"/"email" verbatim
                options.SaveToken = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = auth0.IsConfigured,
                    ValidIssuer = auth0.IsConfigured ? auth0.Issuer : null,
                    ValidateAudience = auth0.IsConfigured,
                    ValidAudience = auth0.Audience,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    // The default is FIVE MINUTES, which is far more slack than a local clock needs
                    // and makes "expired token" behaviour hard to reason about or test.
                    ClockSkew = TimeSpan.FromSeconds(30),
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                    NameClaimType = SubClaim,
                };

                options.Events = new JwtBearerEvents
                {
                    // The default challenge writes an EMPTY body. The API key filter this replaces
                    // returned ProblemDetails, and the SPA parses error bodies, so keep the shape.
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/problem+json";
                        context.Response.Headers.WWWAuthenticate =
                            $"Bearer error=\"{context.Error ?? "invalid_token"}\"";

                        await context.Response.WriteAsync(JsonSerializer.Serialize(new
                        {
                            type = "https://tools.ietf.org/html/rfc9110#section-15.5.2",
                            title = "Unauthorized",
                            status = StatusCodes.Status401Unauthorized,
                            detail = context.ErrorDescription,
                        }));
                    },

                    // Without this, a JWKS fetch that cannot leave the host looks identical to a
                    // bad token: every request 401s and nothing says why.
                    OnAuthenticationFailed = context =>
                    {
                        context.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>()
                            .CreateLogger("Auth0")
                            .LogDebug(context.Exception, "JWT validation failed");
                        return Task.CompletedTask;
                    },
                };
            });

        builder.Services.AddAuthorization();
        return builder;
    }
}

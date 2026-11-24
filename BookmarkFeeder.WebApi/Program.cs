using Scalar.AspNetCore;
using BookmarkFeeder.WebApi.Auth;
using BookmarkFeeder.WebApi.Data;
using BookmarkFeeder.WebApi.Endpoints;
using BookmarkFeeder.WebApi.Filters;
using BookmarkFeeder.WebApi.Services;
using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Register the DbContext through a single factory-owned pattern. Services resolve
// IDbContextFactory<BookmarkDbContext> (docs: factory pattern, no generic repositories);
// a scoped context sourced from the factory keeps health checks and scoped consumers working.
// Scoped, NOT the default singleton: BookmarkDbContext's constructor takes the scoped
// ICurrentUserContext that drives the owner query filter, and EF resolves constructor
// dependencies from the provider the FACTORY was resolved from. A singleton factory holds the
// root provider and would throw "Cannot resolve scoped service from root provider".
builder.Services.AddDbContextFactory<BookmarkDbContext>((_, options) =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("bookmarkfeeder"),
        npgsql => npgsql.EnableRetryOnFailure()),
    lifetime: ServiceLifetime.Scoped);

builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IDbContextFactory<BookmarkDbContext>>().CreateDbContext());

// Add health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<BookmarkDbContext>("database");

builder.Services.AddProblemDetails();

// Trust X-Forwarded-For/Proto from the gateway so the API sees the real scheme/client IP.
// KnownProxies/Networks are cleared because the gateway sits on an internal container network.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Identity: two authentication schemes (Auth0 bearer for the web app, minted API keys for the
// extension) plus the per-request resolution of who the caller is.
builder.AddBookmarkFeederAuthentication();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ICurrentUserContext, CurrentUserContext>();
builder.Services.AddScoped<IUserProvisioningService, UserProvisioningService>();
builder.Services.AddScoped<IApiKeyService, ApiKeyService>();
builder.Services.AddScoped<CurrentUserEndpointFilter>();
builder.Services.AddScoped<ApiKeyScopeFilter>();

// Application services (consume IDbContextFactory<BookmarkDbContext>).
builder.Services.AddScoped<IBookmarkService, BookmarkService>();
builder.Services.AddScoped<ITagService, TagService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<ISavedSearchService, SavedSearchService>();

// Favicon enrichment: a queue the API writes to and a background worker that drains it.
// Opt-out via Favicon:Enabled=false — the test suites turn it off so they never reach the
// network, and self-hosters can disable the outbound fetching entirely.
builder.Services.AddSingleton<IFaviconQueue>(_ => new FaviconQueue());
builder.Services.AddScoped<IFaviconResolver, FaviconResolver>();
#pragma warning disable EXTEXP0001 // RemoveAllResilienceHandlers, below
builder.Services.AddHttpClient(FaviconResolver.HttpClientName, client =>
{
    // Short: a slow site must not occupy a worker slot for long.
    client.Timeout = TimeSpan.FromSeconds(10);
    // Some sites serve no icon markup to unknown agents.
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "Mozilla/5.0 (compatible; BookmarkFeeder/1.0; +https://github.com/bookmarkfeeder)");
    // A favicon is small; refuse to buffer a page that isn't.
    client.MaxResponseContentBufferSize = 2 * 1024 * 1024;
})
// Bookmark URLs are user input: only public addresses may be connected to (see the guard).
.ConfigurePrimaryHttpMessageHandler(() =>
    FaviconNetworkGuard.CreateHandler(builder.Configuration.GetValue("Favicon:AllowPrivateNetworks", false)))
// ServiceDefaults adds a standard resilience handler to every client. Here it is wrong twice over:
// one circuit breaker shared by every site means a burst of dead domains opens it and healthy
// sites then fail (and get stamped as having no favicon), and retries multiply requests to
// servers that are not ours. EXTEXP0001: the API is marked experimental but is the documented way
// to opt one client out of ConfigureHttpClientDefaults.
.RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001

if (builder.Configuration.GetValue("Favicon:Enabled", true))
{
    builder.Services.AddHostedService<FaviconBackgroundService>();
}

// FluentValidation validators.
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
// Declare the X-API-Key security scheme so Scalar/OpenAPI clients show an auth input.
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        var domain = builder.Configuration["Auth0:Domain"];
        var audience = builder.Configuration["Auth0:Audience"];

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

        // Always available: paste a token from any source.
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Auth0 access token, as `Authorization: Bearer <jwt>`."
        };

        // Retained so the docs still describe how the extension authenticates.
        document.Components.SecuritySchemes["ApiKey"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = ApiKeyAuthenticationHandler.HeaderName,
            Description = "A key minted in the web app's Settings. Limited to the sync endpoints."
        };

        // Interactive login, only when a tenant is configured: design-time and GetDocument builds
        // have none, and new Uri("https:///authorize") would throw.
        if (!string.IsNullOrWhiteSpace(domain) && !string.IsNullOrWhiteSpace(audience))
        {
            document.Components.SecuritySchemes["OAuth2"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.OAuth2,
                Flows = new OpenApiOAuthFlows
                {
                    AuthorizationCode = new OpenApiOAuthFlow
                    {
                        // The `audience` parameter is REQUIRED by Auth0. Without it the tenant
                        // issues an opaque token instead of a JWT and every call 401s.
                        AuthorizationUrl = new Uri(
                            $"https://{domain}/authorize?audience={Uri.EscapeDataString(audience)}"),
                        TokenUrl = new Uri($"https://{domain}/oauth/token"),
                        Scopes = new Dictionary<string, string>
                        {
                            ["openid"] = "OIDC sign-in",
                            ["profile"] = "Display name",
                            ["email"] = "Email address",
                        }
                    }
                }
            };
        }

        document.Security ??= [];
        document.Security.Add(
            new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document, null)] = []
            });

        return Task.CompletedTask;
    });
});

// CORS: the extension authenticates with a custom X-API-Key header (not cookies), so no
// credentials are needed. AllowAnyOrigin keeps self-hosted deployments flexible.
// Do NOT add AllowCredentials() - it is illegal alongside AllowAnyOrigin() and would break the
// extension. Bearer and X-API-Key are plain headers, already covered by AllowAnyHeader().
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Per-endpoint rate limiting, partitioned by credential (fallback to forwarded client IP).
// Limits are config-overridable (RateLimiting:{Sync,Writes,Reads}); defaults below.
var readsLimit = builder.Configuration.GetValue<int?>("RateLimiting:Reads") ?? 200;
var writesLimit = builder.Configuration.GetValue<int?>("RateLimiting:Writes") ?? 100;
var syncLimit = builder.Configuration.GetValue<int?>("RateLimiting:Sync") ?? 5;

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, _) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
        }
        await ValueTask.CompletedTask;
    };

    options.AddPolicy("reads", ctx => FixedWindow(ClientPartitionKey(ctx), readsLimit));
    options.AddPolicy("writes", ctx => FixedWindow(ClientPartitionKey(ctx), writesLimit));
    options.AddPolicy("sync", ctx => FixedWindow(ClientPartitionKey(ctx), syncLimit));
});

var app = builder.Build();

// Must run first so downstream middleware sees the gateway-forwarded scheme/IP.
app.UseForwardedHeaders();

app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        var clientId = app.Configuration["Auth0:ClientId"];
        if (!string.IsNullOrWhiteSpace(clientId))
        {
            options.AddPreferredSecuritySchemes("OAuth2")
                   .AddAuthorizationCodeFlow("OAuth2", flow =>
                   {
                       // Public SPA client: PKCE, no secret.
                       flow.ClientId = clientId;
                       flow.Pkce = Pkce.Sha256;
                       flow.SelectedScopes = ["openid", "profile", "email"];
                   })
                   .WithPersistentAuthentication();
        }
        else
        {
            // No tenant configured; fall back to pasting a token by hand.
            options.AddPreferredSecuritySchemes("Bearer");
        }
    });
}

// HTTPS redirect is opt-in (config `Https:Redirect`, default off): behind the gateway the API
// runs plain HTTP, and forcing HTTPS there would break requests. Enable only when TLS is
// terminated at the API itself.
if (app.Configuration.GetValue<bool>("Https:Redirect"))
{
    app.UseHttpsRedirection();
}

// CORS is retained solely for the browser extension (cross-origin). The web app is same-origin
// via the gateway and does not rely on it.
// Before authentication: a CORS preflight carries no credentials and must be answered by the
// CORS middleware, not challenged by authorization.
app.UseCors();

// Authentication populates HttpContext.User but never short-circuits.
app.UseAuthentication();

// Deliberately BETWEEN authentication and authorization. After authentication, so the partition
// key can see who is calling; before authorization, because AuthorizationMiddleware short-circuits
// - if the limiter ran last, an unauthenticated caller could hammer token validation and key
// lookups with no rate limiting at all, which is worse than the shared-key model it replaces.
app.UseRateLimiter();

app.UseAuthorization();

app.MapDefaultEndpoints();

// Fail fast if the API is exposed without an identity provider outside development. Skipped
// during design-time tooling (EF migrations, OpenAPI GetDocument) which runs with no tenant.
if (!IsDesignTimeBuild() && !EF.IsDesignTime && !app.Environment.IsDevelopment())
{
    var missing = new List<string>();
    if (string.IsNullOrWhiteSpace(app.Configuration["Auth0:Domain"])) missing.Add("Auth0:Domain");
    if (string.IsNullOrWhiteSpace(app.Configuration["Auth0:Audience"])) missing.Add("Auth0:Audience");

    if (missing.Count > 0)
    {
        throw new InvalidOperationException(
            $"{string.Join(" and ", missing)} must be configured (user-secrets, environment, or Aspire " +
            "parameter) outside Development. Every /api endpoint requires an Auth0 token or a minted API key.");
    }
}

// Public by design: the SPA and the extension need the tenant coordinates BEFORE they can obtain a
// token, so this cannot sit behind RequireAuthorization. None of it is secret - an OAuth public
// client id is published in every SPA bundle. Mapped on `app`, so it joins neither the authorized
// group nor its filters.
app.MapGet("/api/config", (IOptions<Auth0Options> auth0) => TypedResults.Ok(new
    {
        auth0 = new
        {
            domain = auth0.Value.Domain,
            clientId = auth0.Value.ClientId,
            audience = auth0.Value.Audience,
        }
    }))
   .WithTags("Config")
   .AllowAnonymous()
   .RequireRateLimiting("reads");

// Every /api endpoint requires a credential: an Auth0 bearer token, or a minted API key.
// CurrentUserEndpointFilter then resolves which account is calling, and ApiKeyScopeFilter confines
// API keys to the handful of endpoints marked [AllowApiKey]. "reads" is the group-default
// rate-limit policy; mutating and batch endpoints override it (see the endpoint definitions).
var api = app.MapGroup("/api")
    .RequireAuthorization()
    .AddEndpointFilter<CurrentUserEndpointFilter>()
    .AddEndpointFilter<ApiKeyScopeFilter>()
    .RequireRateLimiting("reads");
api.MapGroup("/bookmarks").MapBookmarkEndpoints().WithTags("Bookmarks");
api.MapGroup("/tags").MapTagEndpoints().WithTags("Tags");
api.MapGroup("/categories").MapCategoryEndpoints().WithTags("Categories");
api.MapGroup("/searches").MapSavedSearchEndpoints().WithTags("Saved searches");
api.MapGroup("/keys").MapApiKeyEndpoints().WithTags("API keys");

// Initialize database (skip during design-time builds)
if (!IsDesignTimeBuild())
{
    await InitializeDatabaseAsync(app);
}

app.Run();

static RateLimitPartition<string> FixedWindow(string partitionKey, int permitLimit) =>
    RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = permitLimit,
        Window = TimeSpan.FromMinutes(1),
    });

// Partitioned PER CREDENTIAL, so a runaway extension cannot exhaust the bucket the web app is
// using. Prefixes keep the three key spaces from colliding.
static string ClientPartitionKey(HttpContext context)
{
    // An API key: limit the key itself, not its owner.
    var keyId = context.User.FindFirst(ApiKeyAuthenticationHandler.KeyIdClaim)?.Value;
    if (!string.IsNullOrEmpty(keyId)) return $"key:{keyId}";

    // A signed-in human. MapInboundClaims is off, so "sub" survives verbatim. This is the
    // VALIDATED subject - never read it from an unverified header, or anyone could forge a
    // partition key and evict a real user's bucket.
    var sub = context.User.FindFirst(AuthenticationExtensions.SubClaim)?.Value;
    if (!string.IsNullOrEmpty(sub)) return $"user:{sub}";

    // Unauthenticated, or a rejected credential. UseForwardedHeaders has already run, so this is
    // the real client address rather than the gateway's.
    return $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "anonymous"}";
}

static bool IsDesignTimeBuild()
{
    // NOT DOTNET_RUNNING_IN_CONTAINER. Every .NET container image sets it, so including it here
    // made this return true in production, which skipped InitializeDatabaseAsync — migrations
    // never ran, the database was never created, and every request died on
    // 3D000: database "bookmarkfeeder" does not exist. Running in a container is the opposite of
    // a design-time build. Latent since the first API commit because nothing had ever been
    // deployed to one.
    return Environment.GetEnvironmentVariable("EF_DESIGN_MODE") == "True" ||
           Environment.GetCommandLineArgs().Any(arg => arg.Contains("GetDocument"));
}

static async Task InitializeDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<BookmarkDbContext>>();

    using var context = factory.CreateDbContext();

    try
    {
        // Apply any pending migrations
        await context.Database.MigrateAsync();

        // Seed development data if in development environment
        if (app.Environment.IsDevelopment())
        {
            await SeedData.SeedDevelopmentDataAsync(context);
        }
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while initializing the database");
        throw;
    }
}

// Exposed so WebApplicationFactory<Program> can bootstrap the app in integration tests.
public partial class Program;
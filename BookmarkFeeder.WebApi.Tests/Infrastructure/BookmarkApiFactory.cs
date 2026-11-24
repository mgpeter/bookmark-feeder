using System.Net.Http.Headers;
using BookmarkFeeder.WebApi.Auth;
using BookmarkFeeder.WebApi.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace BookmarkFeeder.WebApi.Tests.Infrastructure;

/// <summary>
/// Boots the real WebApi pipeline against an EF in-memory database, authenticating with locally
/// signed tokens rather than a live Auth0 tenant.
/// NOTE: EF InMemory does NOT enforce the unique Url index; duplicate detection is exercised
/// here via the service's own pre-check query, but true constraint enforcement needs Postgres.
/// </summary>
public class BookmarkApiFactory : WebApplicationFactory<Program>
{
    /// <summary>The subject CreateAuthenticatedClient() authenticates as unless told otherwise.</summary>
    public const string DefaultSub = "auth0|test-user-a";

    /// <summary>A second identity, for proving that one account cannot see another's data.</summary>
    public const string OtherSub = "auth0|test-user-b";

    private readonly string _databaseName = $"bookmarkfeeder-tests-{Guid.NewGuid()}";

    /// <summary>Extra configuration for one test; set before the first client or context.</summary>
    public Dictionary<string, string?> Settings { get; } = [];

    public BookmarkApiFactory()
    {
        // Skip the startup MigrateAsync/seed path (InMemory is not relational) and the fail-fast.
        Environment.SetEnvironmentVariable("EF_DESIGN_MODE", "True");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // A tenant that does not exist. It is never contacted: ConfigureServices below swaps in a
        // symmetric signing key and clears the metadata lookup, so no JWKS fetch is attempted.
        builder.UseSetting("Auth0:Domain", "bookmarkfeeder-tests.local");
        builder.UseSetting("Auth0:Audience", TestTokens.Audience);

        // Effectively disable rate limiting for the general test suite; the dedicated
        // RateLimitTests override these with low values to exercise 429 behavior.
        builder.UseSetting("RateLimiting:Reads", "1000000");
        builder.UseSetting("RateLimiting:Writes", "1000000");
        builder.UseSetting("RateLimiting:Sync", "1000000");

        // No background favicon fetching in tests — it would make real HTTP requests to the
        // seeded bookmarks' sites. The worker is tested directly in FaviconBackgroundServiceTests.
        builder.UseSetting("Favicon:Enabled", "false");

        foreach (var (key, value) in Settings) builder.UseSetting(key, value);

        builder.ConfigureServices(services =>
        {
            ConfigureTestJwtBearer(services);

            services.RemoveAll<IDbContextFactory<BookmarkDbContext>>();
            services.RemoveAll<DbContextOptions<BookmarkDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<BookmarkDbContext>();
            // EF Core 9/10 stores the provider config here; must be removed or UseNpgsql
            // and UseInMemory both apply, registering two database providers.
            services.RemoveAll(typeof(IDbContextOptionsConfiguration<BookmarkDbContext>));

            // ServiceLifetime.Scoped must mirror Program.cs: BookmarkDbContext's constructor takes
            // the scoped ICurrentUserContext, and a singleton factory holds the ROOT provider,
            // which cannot resolve it.
            services.AddDbContextFactory<BookmarkDbContext>(
                options => options.UseInMemoryDatabase(_databaseName),
                ServiceLifetime.Scoped);
            services.AddScoped(sp =>
                sp.GetRequiredService<IDbContextFactory<BookmarkDbContext>>().CreateDbContext());
        });
    }

    /// <summary>
    /// Runs after the application's own AddJwtBearer, so it wins. Clearing Authority,
    /// MetadataAddress and ConfigurationManager while supplying an explicit IssuerSigningKey is
    /// what keeps the handler entirely off the network.
    /// </summary>
    internal static void ConfigureTestJwtBearer(IServiceCollection services)
    {
        services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.Authority = null;
            options.MetadataAddress = null;
            options.RequireHttpsMetadata = false;
            options.ConfigurationManager = null;
            options.TokenValidationParameters.ValidateIssuer = true;
            options.TokenValidationParameters.ValidIssuer = TestTokens.Issuer;
            options.TokenValidationParameters.ValidateAudience = true;
            options.TokenValidationParameters.ValidAudience = TestTokens.Audience;
            options.TokenValidationParameters.IssuerSigningKey = TestTokens.SigningKey;
            options.TokenValidationParameters.ValidAlgorithms = [SecurityAlgorithms.HmacSha256];
        });
    }

    public HttpClient CreateAuthenticatedClient(string sub = DefaultSub) =>
        WithBearer(CreateClient(), sub);

    public HttpClient CreateAuthenticatedClient(
        WebApplicationFactoryClientOptions options, string sub = DefaultSub) =>
        WithBearer(CreateClient(options), sub);

    /// <summary>A client authenticating the way the browser extension does.</summary>
    public HttpClient CreateKeyClient(string apiKey)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", apiKey);
        return client;
    }

    /// <summary>A client carrying a token this app will reject, for negative tests.</summary>
    public HttpClient CreateClientWithRawToken(string token)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static HttpClient WithBearer(HttpClient client, string sub)
    {
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokens.Create(sub));
        return client;
    }

    /// <summary>
    /// A context for direct assertions, sourced from a scope with an explicit owner. The scope is
    /// intentionally not disposed: the returned context owns it for the caller's lifetime.
    /// Passing null yields an UNSCOPED context (owner Guid.Empty), which sees nothing — combine it
    /// with IgnoreQueryFilters(["Owner"]) when asserting across accounts.
    /// </summary>
    public BookmarkDbContext CreateContext(Guid? ownerId = null)
    {
        var scope = Services.CreateScope();

        if (ownerId is { } id && id != Guid.Empty)
        {
            scope.ServiceProvider.GetRequiredService<ICurrentUserContext>().Set(id);
        }

        return scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<BookmarkDbContext>>()
            .CreateDbContext();
    }

    /// <summary>
    /// The user id a subject was provisioned as. Only meaningful after that subject has made at
    /// least one request, since accounts are created just-in-time.
    /// </summary>
    public async Task<Guid> OwnerIdForAsync(string sub = DefaultSub)
    {
        await using var context = CreateContext();
        return await context.Users
            .Where(u => u.Auth0Sub == sub)
            .Select(u => u.Id)
            .FirstAsync();
    }
}

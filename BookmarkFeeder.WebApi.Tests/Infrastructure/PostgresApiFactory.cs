using System.Net.Http.Headers;
using BookmarkFeeder.WebApi.Auth;
using BookmarkFeeder.WebApi.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace BookmarkFeeder.WebApi.Tests.Infrastructure;

/// <summary>
/// Boots the real WebApi pipeline against a disposable PostgreSQL container (Testcontainers),
/// so behaviors EF InMemory can't model — the per-owner unique indexes, full-text search,
/// soft-delete × unique interaction, and FK cascade/SetNull — are exercised against a real
/// database. Authentication uses locally signed tokens; no test contacts Auth0.
/// </summary>
public class PostgresApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string DefaultSub = "auth0|test-user-a";
    public const string OtherSub = "auth0|test-user-b";

    // Same image and major version the AppHost pins for dev and production. Tests previously ran
    // 17-alpine while production emitted 18.x, so the suite validated a major version nothing
    // shipped — exactly the drift a tsvector/generated-column feature set cannot afford.
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:18.3")
        .Build();

    public async Task InitializeAsync()
    {
        // Skip the app's own startup Migrate/seed; we control migration below.
        Environment.SetEnvironmentVariable("EF_DESIGN_MODE", "True");
        await _container.StartAsync();

        // Building the host triggers ConfigureWebHost (which reads the now-started container's
        // connection string); then apply the real EF migrations.
        using var scope = Services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<BookmarkDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        await db.Database.MigrateAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Auth0:Domain", "bookmarkfeeder-tests.local");
        builder.UseSetting("Auth0:Audience", TestTokens.Audience);
        builder.UseSetting("RateLimiting:Reads", "1000000");
        builder.UseSetting("RateLimiting:Writes", "1000000");
        builder.UseSetting("RateLimiting:Sync", "1000000");

        // No background favicon fetching in tests — it would make real HTTP requests to the
        // seeded bookmarks' sites. The worker is tested directly in FaviconBackgroundServiceTests.
        builder.UseSetting("Favicon:Enabled", "false");

        builder.ConfigureServices(services =>
        {
            BookmarkApiFactory.ConfigureTestJwtBearer(services);

            services.RemoveAll<IDbContextFactory<BookmarkDbContext>>();
            services.RemoveAll<DbContextOptions<BookmarkDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<BookmarkDbContext>();
            services.RemoveAll(typeof(IDbContextOptionsConfiguration<BookmarkDbContext>));

            // Scoped, mirroring Program.cs — BookmarkDbContext needs the scoped ICurrentUserContext.
            services.AddDbContextFactory<BookmarkDbContext>(
                options => options.UseNpgsql(_container.GetConnectionString()),
                ServiceLifetime.Scoped);
            services.AddScoped(sp =>
                sp.GetRequiredService<IDbContextFactory<BookmarkDbContext>>().CreateDbContext());
        });
    }

    public HttpClient CreateAuthenticatedClient(string sub = DefaultSub)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokens.Create(sub));
        return client;
    }

    /// <summary>A client authenticating the way the browser extension does.</summary>
    public HttpClient CreateKeyClient(string apiKey)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", apiKey);
        return client;
    }

    /// <summary>
    /// A context for direct assertions, sourced from a scope with an explicit owner. Passing null
    /// yields an UNSCOPED context (owner Guid.Empty) that sees nothing — combine it with
    /// IgnoreQueryFilters(["Owner"]) when asserting across accounts.
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

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _container.DisposeAsync();
        await base.DisposeAsync();
    }
}

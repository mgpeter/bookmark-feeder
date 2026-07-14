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
/// so behaviors EF InMemory can't model — the unique Url index, ILIKE search, soft-delete ×
/// unique interaction, and FK cascade/SetNull — are exercised against a real database.
/// </summary>
public class PostgresApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string TestApiKey = "test-key";

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
        builder.UseSetting("Authentication:ApiKey", TestApiKey);
        builder.UseSetting("RateLimiting:Reads", "1000000");
        builder.UseSetting("RateLimiting:Writes", "1000000");
        builder.UseSetting("RateLimiting:Sync", "1000000");

        // No background favicon fetching in tests — it would make real HTTP requests to the
        // seeded bookmarks' sites. The worker is tested directly in FaviconBackgroundServiceTests.
        builder.UseSetting("Favicon:Enabled", "false");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDbContextFactory<BookmarkDbContext>>();
            services.RemoveAll<DbContextOptions<BookmarkDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<BookmarkDbContext>();
            services.RemoveAll(typeof(IDbContextOptionsConfiguration<BookmarkDbContext>));

            services.AddDbContextFactory<BookmarkDbContext>(options =>
                options.UseNpgsql(_container.GetConnectionString()));
            services.AddScoped(sp =>
                sp.GetRequiredService<IDbContextFactory<BookmarkDbContext>>().CreateDbContext());
        });
    }

    public HttpClient CreateAuthenticatedClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", TestApiKey);
        return client;
    }

    public BookmarkDbContext CreateContext() =>
        Services.GetRequiredService<IDbContextFactory<BookmarkDbContext>>().CreateDbContext();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _container.DisposeAsync();
        await base.DisposeAsync();
    }
}

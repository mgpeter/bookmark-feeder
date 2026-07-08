using BookmarkFeeder.WebApi.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BookmarkFeeder.WebApi.Tests.Infrastructure;

/// <summary>
/// Boots the real WebApi pipeline against an EF in-memory database and a known API key.
/// NOTE: EF InMemory does NOT enforce the unique Url index; duplicate detection is exercised
/// here via the service's own pre-check query, but true constraint enforcement needs Postgres.
/// </summary>
public class BookmarkApiFactory : WebApplicationFactory<Program>
{
    public const string TestApiKey = "test-key";

    private readonly string _databaseName = $"bookmarkfeeder-tests-{Guid.NewGuid()}";

    public BookmarkApiFactory()
    {
        // Skip the startup MigrateAsync/seed path (InMemory is not relational) and the fail-fast.
        Environment.SetEnvironmentVariable("EF_DESIGN_MODE", "True");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Authentication:ApiKey", TestApiKey);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDbContextFactory<BookmarkDbContext>>();
            services.RemoveAll<DbContextOptions<BookmarkDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<BookmarkDbContext>();
            // EF Core 9/10 stores the provider config here; must be removed or UseNpgsql
            // and UseInMemory both apply, registering two database providers.
            services.RemoveAll(typeof(IDbContextOptionsConfiguration<BookmarkDbContext>));

            services.AddDbContextFactory<BookmarkDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
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
}

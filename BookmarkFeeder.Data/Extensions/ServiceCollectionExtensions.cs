using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using BookmarkFeeder.Data.Configuration;
using BookmarkFeeder.Data.Context;

namespace BookmarkFeeder.Data.Extensions;

/// <summary>
/// Extension methods for registering data layer services with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
  /// <summary>
  /// Registers BookmarkFeeder data services including DbContext and configuration options.
  /// </summary>
  /// <param name="services">The service collection to add services to.</param>
  /// <param name="configuration">The configuration instance.</param>
  /// <returns>The service collection for chaining.</returns>
  public static IServiceCollection AddBookmarkFeederData(
    this IServiceCollection services,
    IConfiguration configuration)
  {
    // Register the custom validator
    services.AddSingleton<IValidateOptions<DatabaseOptions>, DatabaseOptionsValidator>();

    // Configure database options with validation
    services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName))
      .AddOptions<DatabaseOptions>()
      .Bind(configuration.GetSection(DatabaseOptions.SectionName))
      .ValidateDataAnnotations()
      .ValidateOnStart();

    // Register DbContext with PostgreSQL provider
    services.AddDbContext<BookmarkFeederDbContext>((serviceProvider, options) =>
    {
      var databaseOptions = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
      
      options.UseNpgsql(databaseOptions.ConnectionString, npgsqlOptions =>
      {
        npgsqlOptions.EnableRetryOnFailure(
          maxRetryCount: databaseOptions.MaxRetryCount,
          maxRetryDelay: TimeSpan.FromSeconds(30),
          errorCodesToAdd: null);
        
        npgsqlOptions.CommandTimeout(databaseOptions.CommandTimeout);
      });

      // Configure logging and error handling based on options
      if (databaseOptions.EnableDetailedErrors)
      {
        options.EnableDetailedErrors();
      }

      if (databaseOptions.EnableSensitiveDataLogging)
      {
        options.EnableSensitiveDataLogging();
      }

      // Enable service provider caching for better performance
      options.EnableServiceProviderCaching();
    });

    // Register health checks for the database
    services.AddHealthChecks()
      .AddDbContextCheck<BookmarkFeederDbContext>(
        name: "bookmarkfeeder-database",
        failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
        tags: new[] { "database", "postgresql" });

    return services;
  }

  /// <summary>
  /// Registers BookmarkFeeder data services with a pre-configured connection string.
  /// Useful for testing scenarios.
  /// </summary>
  /// <param name="services">The service collection to add services to.</param>
  /// <param name="connectionString">The database connection string.</param>
  /// <returns>The service collection for chaining.</returns>
  public static IServiceCollection AddBookmarkFeederDataWithConnectionString(
    this IServiceCollection services,
    string connectionString)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

    // Configure database options with test defaults
    services.Configure<DatabaseOptions>(options =>
    {
      options.ConnectionString = connectionString;
      options.MaxRetryCount = 3;
      options.CommandTimeout = 30;
      options.EnableDetailedErrors = true;
      options.EnableSensitiveDataLogging = false;
      options.PoolSize = 10;
      options.AutoMigrateOnStartup = false;
    });

    // Validate the options
    services.AddOptions<DatabaseOptions>()
      .ValidateDataAnnotations()
      .ValidateOnStart();

    // Register DbContext
    services.AddDbContext<BookmarkFeederDbContext>((serviceProvider, options) =>
    {
      var databaseOptions = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
      
      options.UseNpgsql(connectionString, npgsqlOptions =>
      {
        npgsqlOptions.EnableRetryOnFailure(
          maxRetryCount: databaseOptions.MaxRetryCount,
          maxRetryDelay: TimeSpan.FromSeconds(30),
          errorCodesToAdd: null);
        
        npgsqlOptions.CommandTimeout(databaseOptions.CommandTimeout);
      });

      options.EnableDetailedErrors(databaseOptions.EnableDetailedErrors);
      options.EnableSensitiveDataLogging(databaseOptions.EnableSensitiveDataLogging);
      options.EnableServiceProviderCaching();
    });

    return services;
  }
}
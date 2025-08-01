using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using BookmarkFeeder.Data.Configuration;
using BookmarkFeeder.Data.Models;

namespace BookmarkFeeder.Data.Context;

/// <summary>
/// Entity Framework DbContext for BookmarkFeeder application.
/// Provides access to database entities and manages database operations.
/// </summary>
public class BookmarkFeederDbContext : DbContext
{
  private readonly DatabaseOptions _databaseOptions;

  /// <summary>
  /// Initializes a new instance of the BookmarkFeederDbContext with specified options.
  /// </summary>
  /// <param name="options">The options for this context.</param>
  /// <param name="databaseOptions">Database configuration options.</param>
  public BookmarkFeederDbContext(
    DbContextOptions<BookmarkFeederDbContext> options,
    IOptions<DatabaseOptions> databaseOptions) : base(options)
  {
    _databaseOptions = databaseOptions.Value;
  }

  /// <summary>
  /// Parameterless constructor for design-time support.
  /// Should not be used in runtime scenarios.
  /// </summary>
  public BookmarkFeederDbContext()
  {
    _databaseOptions = new DatabaseOptions();
  }

  // Entity DbSets
  public DbSet<Bookmark> Bookmarks => Set<Bookmark>();
  public DbSet<Tag> Tags => Set<Tag>();
  public DbSet<BookmarkTag> BookmarkTags => Set<BookmarkTag>();

  /// <summary>
  /// Configures the database context options.
  /// </summary>
  /// <param name="optionsBuilder">The builder being used to configure the context.</param>
  protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
  {
    if (!optionsBuilder.IsConfigured)
    {
      // This configuration is used for design-time scenarios
      // In runtime, configuration is handled through dependency injection
      optionsBuilder.UseNpgsql("Server=localhost;Database=BookmarkFeeder;User Id=postgres;Password=postgres;");
    }

    // Configure command timeout
    optionsBuilder.EnableServiceProviderCaching()
      .EnableSensitiveDataLogging(false);

    base.OnConfiguring(optionsBuilder);
  }

  /// <summary>
  /// Configures the model that was discovered by convention from the entity types.
  /// </summary>
  /// <param name="modelBuilder">The builder being used to configure the model.</param>
  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    // Configure entity mappings here when entities are added
    // Apply all configurations from the current assembly
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(BookmarkFeederDbContext).Assembly);

    base.OnModelCreating(modelBuilder);
  }

  /// <summary>
  /// Saves all changes made in this context to the database.
  /// </summary>
  /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
  /// <returns>A task that represents the asynchronous save operation.</returns>
  public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
  {
    try
    {
      return await base.SaveChangesAsync(cancellationToken);
    }
    catch (Exception ex)
    {
      // Log the exception and re-throw
      // Logging will be implemented when entities are added
      throw new InvalidOperationException("An error occurred while saving changes to the database.", ex);
    }
  }

  /// <summary>
  /// Checks if the database can be connected to.
  /// </summary>
  /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
  /// <returns>True if the database can be connected to, otherwise false.</returns>
  public async Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
  {
    try
    {
      return await Database.CanConnectAsync(cancellationToken);
    }
    catch
    {
      return false;
    }
  }
}
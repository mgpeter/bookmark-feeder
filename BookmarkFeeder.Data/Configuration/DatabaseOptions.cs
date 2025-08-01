using System.ComponentModel.DataAnnotations;

namespace BookmarkFeeder.Data.Configuration;

/// <summary>
/// Configuration options for database connection and behavior.
/// Uses the Options pattern for configuration management.
/// </summary>
public class DatabaseOptions
{
  /// <summary>
  /// The configuration section name for database options.
  /// </summary>
  public const string SectionName = "Database";

  /// <summary>
  /// Gets or sets the database connection string.
  /// </summary>
  [Required(ErrorMessage = "Database connection string is required.")]
  [MinLength(1, ErrorMessage = "Database connection string cannot be empty.")]
  public string ConnectionString { get; set; } = string.Empty;

  /// <summary>
  /// Gets or sets the maximum number of retry attempts for database operations.
  /// </summary>
  [Range(1, 10, ErrorMessage = "MaxRetryCount must be between 1 and 10.")]
  public int MaxRetryCount { get; set; } = 3;

  /// <summary>
  /// Gets or sets the command timeout in seconds for database operations.
  /// </summary>
  [Range(1, 300, ErrorMessage = "CommandTimeout must be between 1 and 300 seconds.")]
  public int CommandTimeout { get; set; } = 30;

  /// <summary>
  /// Gets or sets whether to enable detailed error logging for database operations.
  /// Should be disabled in production for security reasons.
  /// </summary>
  public bool EnableDetailedErrors { get; set; } = false;

  /// <summary>
  /// Gets or sets whether to enable sensitive data logging.
  /// Should be disabled in production for security reasons.
  /// </summary>
  public bool EnableSensitiveDataLogging { get; set; } = false;

  /// <summary>
  /// Gets or sets the connection pool size.
  /// </summary>
  [Range(1, 100, ErrorMessage = "PoolSize must be between 1 and 100.")]
  public int PoolSize { get; set; } = 10;

  /// <summary>
  /// Gets or sets whether to automatically apply migrations on startup.
  /// </summary>
  public bool AutoMigrateOnStartup { get; set; } = true;
}
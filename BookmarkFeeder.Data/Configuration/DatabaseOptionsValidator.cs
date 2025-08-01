using Microsoft.Extensions.Options;
using Npgsql;

namespace BookmarkFeeder.Data.Configuration;

/// <summary>
/// Validator for DatabaseOptions that provides additional validation beyond data annotations.
/// </summary>
public class DatabaseOptionsValidator : IValidateOptions<DatabaseOptions>
{
  /// <summary>
  /// Validates the database options.
  /// </summary>
  /// <param name="name">The name of the options instance being validated.</param>
  /// <param name="options">The options instance to validate.</param>
  /// <returns>Validation result containing any validation failures.</returns>
  public ValidateOptionsResult Validate(string? name, DatabaseOptions options)
  {
    var failures = new List<string>();

    // Validate connection string format
    if (!string.IsNullOrWhiteSpace(options.ConnectionString))
    {
      try
      {
        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(options.ConnectionString);
        
        // Validate required connection string components
        if (string.IsNullOrWhiteSpace(connectionStringBuilder.Host))
        {
          failures.Add("Connection string must specify a valid Host/Server.");
        }

        if (string.IsNullOrWhiteSpace(connectionStringBuilder.Database))
        {
          failures.Add("Connection string must specify a valid Database name.");
        }

        // Warn about security settings in connection string
        if (connectionStringBuilder.IncludeErrorDetail == true || 
            connectionStringBuilder.LogParameters == true)
        {
          failures.Add("Connection string should not enable error details or parameter logging in production.");
        }
      }
      catch (ArgumentException ex)
      {
        failures.Add($"Invalid connection string format: {ex.Message}");
      }
    }

    // Validate logical combinations of options
    if (options.EnableSensitiveDataLogging && !options.EnableDetailedErrors)
    {
      failures.Add("EnableSensitiveDataLogging requires EnableDetailedErrors to be true.");
    }

    // Validate timeout and retry settings are reasonable
    if (options.CommandTimeout < options.MaxRetryCount * 10)
    {
      failures.Add("CommandTimeout should be at least 10 times the MaxRetryCount to allow for proper retry handling.");
    }

    return failures.Count > 0 
      ? ValidateOptionsResult.Fail(failures)
      : ValidateOptionsResult.Success;
  }
}
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;
using BookmarkFeeder.Data.Context;
using BookmarkFeeder.Data.Configuration;
using BookmarkFeeder.Data.Extensions;

namespace BookmarkFeeder.Data.Tests;

public class DbContextTests
{
  [Fact]
  public void BookmarkFeederDbContext_ShouldInheritFromDbContext()
  {
    // Arrange & Act
    var dbContextType = typeof(BookmarkFeederDbContext);

    // Assert
    dbContextType.BaseType.ShouldBe(typeof(DbContext));
  }

  [Fact]
  public void BookmarkFeederDbContext_ShouldHaveParameterlessConstructor()
  {
    // Arrange & Act
    var dbContextType = typeof(BookmarkFeederDbContext);
    var parameterlessConstructor = dbContextType.GetConstructor(Type.EmptyTypes);

    // Assert
    parameterlessConstructor.ShouldNotBeNull();
  }

  [Fact]
  public void BookmarkFeederDbContext_ShouldHaveDbContextOptionsConstructor()
  {
    // Arrange & Act
    var dbContextType = typeof(BookmarkFeederDbContext);
    var optionsConstructor = dbContextType.GetConstructor(new[] { 
      typeof(DbContextOptions<BookmarkFeederDbContext>),
      typeof(IOptions<DatabaseOptions>)
    });

    // Assert
    optionsConstructor.ShouldNotBeNull();
  }

  [Fact]
  public void DatabaseOptions_ShouldHaveRequiredProperties()
  {
    // Arrange & Act
    var optionsType = typeof(DatabaseOptions);
    var connectionStringProperty = optionsType.GetProperty("ConnectionString");
    var maxRetryCountProperty = optionsType.GetProperty("MaxRetryCount");
    var commandTimeoutProperty = optionsType.GetProperty("CommandTimeout");

    // Assert
    connectionStringProperty.ShouldNotBeNull();
    connectionStringProperty.PropertyType.ShouldBe(typeof(string));
    
    maxRetryCountProperty.ShouldNotBeNull();
    maxRetryCountProperty.PropertyType.ShouldBe(typeof(int));
    
    commandTimeoutProperty.ShouldNotBeNull();
    commandTimeoutProperty.PropertyType.ShouldBe(typeof(int));
  }

  [Fact]
  public void DatabaseOptions_ShouldHaveValidationAttributes()
  {
    // Arrange & Act
    var optionsType = typeof(DatabaseOptions);
    var connectionStringProperty = optionsType.GetProperty("ConnectionString");
    var attributes = connectionStringProperty?.GetCustomAttributes(true);

    // Assert
    attributes.ShouldNotBeNull();
    attributes.Length.ShouldBeGreaterThan(0);
  }

  [Fact]
  public void ServiceCollectionExtensions_ShouldRegisterDbContext()
  {
    // Arrange
    var services = new ServiceCollection();
    var configuration = new ConfigurationBuilder()
      .AddInMemoryCollection(new Dictionary<string, string?>
      {
        ["Database:ConnectionString"] = "Server=localhost;Database=test;User Id=test;Password=test;",
        ["Database:MaxRetryCount"] = "3",
        ["Database:CommandTimeout"] = "30"
      })
      .Build();

    // Act
    services.AddBookmarkFeederData(configuration);
    var serviceProvider = services.BuildServiceProvider();

    // Assert
    var dbContext = serviceProvider.GetService<BookmarkFeederDbContext>();
    dbContext.ShouldNotBeNull();
  }

  [Fact]
  public void ServiceCollectionExtensions_ShouldRegisterDatabaseOptions()
  {
    // Arrange
    var services = new ServiceCollection();
    var configuration = new ConfigurationBuilder()
      .AddInMemoryCollection(new Dictionary<string, string?>
      {
        ["Database:ConnectionString"] = "Server=localhost;Database=test;User Id=test;Password=test;",
        ["Database:MaxRetryCount"] = "5",
        ["Database:CommandTimeout"] = "60"
      })
      .Build();

    // Act
    services.AddBookmarkFeederData(configuration);
    var serviceProvider = services.BuildServiceProvider();

    // Assert
    var options = serviceProvider.GetService<IOptions<DatabaseOptions>>();
    options.ShouldNotBeNull();
    options.Value.ConnectionString.ShouldBe("Server=localhost;Database=test;User Id=test;Password=test;");
    options.Value.MaxRetryCount.ShouldBe(5);
    options.Value.CommandTimeout.ShouldBe(60);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  public void DatabaseOptions_ShouldValidateConnectionString(string? connectionString)
  {
    // Arrange
    var services = new ServiceCollection();
    var configuration = new ConfigurationBuilder()
      .AddInMemoryCollection(new Dictionary<string, string?>
      {
        ["Database:ConnectionString"] = connectionString ?? "",
        ["Database:MaxRetryCount"] = "3",
        ["Database:CommandTimeout"] = "30"
      })
      .Build();

    // Act & Assert
    var exception = Should.Throw<OptionsValidationException>(() =>
    {
      services.AddBookmarkFeederData(configuration);
      var serviceProvider = services.BuildServiceProvider();
      var options = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>();
      var _ = options.Value; // This triggers validation
    });

    exception.Failures.ShouldContain(f => f.Contains("ConnectionString"));
  }

  [Theory]
  [InlineData(-1)]
  [InlineData(0)]
  [InlineData(11)]
  public void DatabaseOptions_ShouldValidateMaxRetryCount(int maxRetryCount)
  {
    // Arrange
    var services = new ServiceCollection();
    var configuration = new ConfigurationBuilder()
      .AddInMemoryCollection(new Dictionary<string, string?>
      {
        ["Database:ConnectionString"] = "Server=localhost;Database=test;User Id=test;Password=test;",
        ["Database:MaxRetryCount"] = maxRetryCount.ToString(),
        ["Database:CommandTimeout"] = "30"
      })
      .Build();

    // Act & Assert
    var exception = Should.Throw<OptionsValidationException>(() =>
    {
      services.AddBookmarkFeederData(configuration);
      var serviceProvider = services.BuildServiceProvider();
      var options = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>();
      var _ = options.Value; // This triggers validation
    });

    exception.Failures.ShouldContain(f => f.Contains("MaxRetryCount"));
  }

  [Theory]
  [InlineData(0)]
  [InlineData(-1)]
  [InlineData(301)]
  public void DatabaseOptions_ShouldValidateCommandTimeout(int commandTimeout)
  {
    // Arrange
    var services = new ServiceCollection();
    var configuration = new ConfigurationBuilder()
      .AddInMemoryCollection(new Dictionary<string, string?>
      {
        ["Database:ConnectionString"] = "Server=localhost;Database=test;User Id=test;Password=test;",
        ["Database:MaxRetryCount"] = "3",
        ["Database:CommandTimeout"] = commandTimeout.ToString()
      })
      .Build();

    // Act & Assert
    var exception = Should.Throw<OptionsValidationException>(() =>
    {
      services.AddBookmarkFeederData(configuration);
      var serviceProvider = services.BuildServiceProvider();
      var options = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>();
      var _ = options.Value; // This triggers validation
    });

    exception.Failures.ShouldContain(f => f.Contains("CommandTimeout"));
  }
}
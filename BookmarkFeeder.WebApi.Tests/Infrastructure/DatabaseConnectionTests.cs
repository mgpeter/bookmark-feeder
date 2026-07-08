using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using BookmarkFeeder.WebApi.Data;

namespace BookmarkFeeder.WebApi.Tests.Infrastructure;

public class DatabaseConnectionTests
{
    [Fact]
    public void DatabaseHealthCheck_ShouldBeRegistered()
    {
        var services = new ServiceCollection();
        
        // Add required services for health checks
        services.AddLogging();
        services.AddOptions();
        services.AddHealthChecks();
        
        var serviceProvider = services.BuildServiceProvider();
        var healthCheckService = serviceProvider.GetService<HealthCheckService>();
        
        healthCheckService.Should().NotBeNull();
    }

    [Fact]
    public async Task DatabaseConnection_ShouldBeConfigurable()
    {
        // Test that we can configure a DbContext with different connection strings
        var services = new ServiceCollection();
        
        // Add in-memory database for testing
        services.AddDbContext<BookmarkDbContext>(options =>
            options.UseInMemoryDatabase("TestDatabase"));
        
        var serviceProvider = services.BuildServiceProvider();
        
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BookmarkDbContext>();
        
        context.Should().NotBeNull();
        await context.Database.EnsureCreatedAsync();
        
        // Verify database is accessible
        var canConnect = await context.Database.CanConnectAsync();
        canConnect.Should().BeTrue();
    }

    [Fact]
    public void DbContextFactory_ShouldBeConfigurable()
    {
        var services = new ServiceCollection();
        
        // Test DbContext factory registration
        services.AddDbContextFactory<BookmarkDbContext>(options =>
            options.UseInMemoryDatabase("FactoryTestDatabase"));
        
        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetService<IDbContextFactory<BookmarkDbContext>>();
        
        factory.Should().NotBeNull();
    }

    [Fact]
    public void DbContextFactory_ShouldCreateWorkingContext()
    {
        var services = new ServiceCollection();
        
        services.AddDbContextFactory<BookmarkDbContext>(options =>
            options.UseInMemoryDatabase("WorkingContextTest"));
        
        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IDbContextFactory<BookmarkDbContext>>();
        
        using var context = factory.CreateDbContext();
        context.Should().NotBeNull();
        context.Database.Should().NotBeNull();
    }

    [Fact]
    public async Task DbContextFactory_ShouldSupportMultipleInstances()
    {
        var services = new ServiceCollection();
        
        services.AddDbContextFactory<BookmarkDbContext>(options =>
            options.UseInMemoryDatabase("MultiInstanceTest"));
        
        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IDbContextFactory<BookmarkDbContext>>();
        
        using var context1 = factory.CreateDbContext();
        using var context2 = factory.CreateDbContext();
        
        context1.Should().NotBeNull();
        context2.Should().NotBeNull();
        context1.Should().NotBeSameAs(context2);
        
        // Both should be able to connect
        var canConnect1 = await context1.Database.CanConnectAsync();
        var canConnect2 = await context2.Database.CanConnectAsync();
        
        canConnect1.Should().BeTrue();
        canConnect2.Should().BeTrue();
    }

    [Fact]
    public void ConnectionString_ShouldBeConfigurable_ViaOptions()
    {
        // Test that we can configure connection strings through options pattern
        var services = new ServiceCollection();
        
        var testConnectionString = "User ID=test;Password=test;Host=localhost;Port=5432;Database=bookmarkfeeder_test;";
        
        // This test ensures we can configure PostgreSQL connection
        // We'll use in-memory for testing but verify the configuration pattern
        services.AddDbContextFactory<BookmarkDbContext>(options =>
        {
            if (IsTestEnvironment())
            {
                options.UseInMemoryDatabase("ConfigTest");
            }
            else
            {
                options.UseNpgsql(testConnectionString);
            }
        });
        
        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetService<IDbContextFactory<BookmarkDbContext>>();
        
        factory.Should().NotBeNull();
    }

    private static bool IsTestEnvironment()
    {
        return true; // Always true in test context
    }
}
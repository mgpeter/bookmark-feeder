using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using BookmarkFeeder.WebApi.Data;
using BookmarkFeeder.WebApi.Models;

namespace BookmarkFeeder.WebApi.Tests.Infrastructure;

public class MigrationAndSeedTests
{
    [Fact]
    public async Task Database_ShouldCreateTablesCorrectly()
    {
        var services = new ServiceCollection();
        
        services.AddDbContextFactory<BookmarkDbContext>(options =>
            options.UseInMemoryDatabase("MigrationTest"));
        
        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IDbContextFactory<BookmarkDbContext>>();
        
        using var context = factory.CreateDbContext();
        
        // Ensure database is created
        var created = await context.Database.EnsureCreatedAsync();
        created.Should().BeTrue();
        
        // Verify all tables/DbSets are accessible
        context.Bookmarks.Should().NotBeNull();
        context.Tags.Should().NotBeNull();
        context.Categories.Should().NotBeNull();
        context.BookmarkTags.Should().NotBeNull();
        
        // Verify we can query each table (which confirms they exist)
        var bookmarks = await context.Bookmarks.ToListAsync();
        var tags = await context.Tags.ToListAsync();
        var categories = await context.Categories.ToListAsync();
        var bookmarkTags = await context.BookmarkTags.ToListAsync();
        
        bookmarks.Should().BeEmpty();
        tags.Should().BeEmpty();
        categories.Should().BeEmpty();
        bookmarkTags.Should().BeEmpty();
    }

    [Fact]
    public async Task Database_ShouldSupportBasicCRUDOperations()
    {
        var services = new ServiceCollection();
        
        services.AddDbContextFactory<BookmarkDbContext>(options =>
            options.UseInMemoryDatabase("CRUDTest"));
        
        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IDbContextFactory<BookmarkDbContext>>();
        
        using var context = factory.CreateDbContext();
        await context.Database.EnsureCreatedAsync();
        
        // Create
        var bookmark = new Bookmark
        {
            Id = Guid.NewGuid(),
            Url = "https://example.com",
            Title = "Test Bookmark",
            Description = "A test bookmark",
            DateAdded = DateTime.UtcNow,
            DateModified = DateTime.UtcNow,
            IsDeleted = false
        };
        
        context.Bookmarks.Add(bookmark);
        await context.SaveChangesAsync();
        
        // Read
        var savedBookmark = await context.Bookmarks.FirstOrDefaultAsync(b => b.Id == bookmark.Id);
        savedBookmark.Should().NotBeNull();
        savedBookmark!.Url.Should().Be("https://example.com");
        savedBookmark.Title.Should().Be("Test Bookmark");
        
        // Update
        savedBookmark.Title = "Updated Test Bookmark";
        savedBookmark.DateModified = DateTime.UtcNow;
        await context.SaveChangesAsync();
        
        var updatedBookmark = await context.Bookmarks.FirstOrDefaultAsync(b => b.Id == bookmark.Id);
        updatedBookmark!.Title.Should().Be("Updated Test Bookmark");
        
        // Delete (soft delete)
        updatedBookmark.IsDeleted = true;
        await context.SaveChangesAsync();
        
        // Verify soft delete works (should not appear in normal queries due to query filter)
        var deletedBookmark = await context.Bookmarks.FirstOrDefaultAsync(b => b.Id == bookmark.Id);
        deletedBookmark.Should().BeNull(); // Due to global query filter
    }

    [Fact]
    public async Task SeedData_ShouldCreateDevelopmentData()
    {
        var services = new ServiceCollection();
        
        services.AddDbContextFactory<BookmarkDbContext>(options =>
            options.UseInMemoryDatabase("SeedDataTest"));
        
        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IDbContextFactory<BookmarkDbContext>>();
        
        using var context = factory.CreateDbContext();
        await context.Database.EnsureCreatedAsync();
        
        // Simulate seed data method
        await SeedDevelopmentDataAsync(context);
        
        // Verify seed data was created
        var bookmarks = await context.Bookmarks.ToListAsync();
        var tags = await context.Tags.ToListAsync();
        var categories = await context.Categories.ToListAsync();
        var bookmarkTags = await context.BookmarkTags.ToListAsync();
        
        bookmarks.Should().NotBeEmpty();
        tags.Should().NotBeEmpty();
        categories.Should().NotBeEmpty();
        bookmarkTags.Should().NotBeEmpty();
        
        // Verify specific seed data
        var techCategory = categories.FirstOrDefault(c => c.Name == "Technology");
        techCategory.Should().NotBeNull();
        
        var dotnetTag = tags.FirstOrDefault(t => t.Name == ".NET");
        dotnetTag.Should().NotBeNull();
        dotnetTag!.NormalizedName.Should().Be(".net");
        
        var dotnetBookmark = bookmarks.FirstOrDefault(b => b.Url.Contains("dotnet"));
        dotnetBookmark.Should().NotBeNull();
    }

    [Fact]
    public async Task SeedData_ShouldBeIdempotent()
    {
        var services = new ServiceCollection();
        
        services.AddDbContextFactory<BookmarkDbContext>(options =>
            options.UseInMemoryDatabase("IdempotentSeedTest"));
        
        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IDbContextFactory<BookmarkDbContext>>();
        
        using var context = factory.CreateDbContext();
        await context.Database.EnsureCreatedAsync();
        
        // Seed data first time
        await SeedDevelopmentDataAsync(context);
        var firstCount = await context.Bookmarks.CountAsync();
        
        // Seed data second time - should not create duplicates
        await SeedDevelopmentDataAsync(context);
        var secondCount = await context.Bookmarks.CountAsync();
        
        secondCount.Should().Be(firstCount);
    }

    [Fact]
    public async Task Database_ShouldSupportComplexQueries()
    {
        var services = new ServiceCollection();
        
        services.AddDbContextFactory<BookmarkDbContext>(options =>
            options.UseInMemoryDatabase("ComplexQueryTest"));
        
        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IDbContextFactory<BookmarkDbContext>>();
        
        using var context = factory.CreateDbContext();
        await context.Database.EnsureCreatedAsync();
        
        // Create test data with relationships
        var techCategory = new Category { Id = Guid.NewGuid(), Name = "Technology", DateCreated = DateTime.UtcNow };
        var csharpTag = new Tag { Id = Guid.NewGuid(), Name = "C#", NormalizedName = "c#", DateCreated = DateTime.UtcNow };
        var dotnetTag = new Tag { Id = Guid.NewGuid(), Name = ".NET", NormalizedName = ".net", DateCreated = DateTime.UtcNow };
        
        var bookmark1 = new Bookmark 
        { 
            Id = Guid.NewGuid(), 
            Url = "https://docs.microsoft.com/dotnet", 
            Title = ".NET Documentation",
            DateAdded = DateTime.UtcNow,
            DateModified = DateTime.UtcNow
        };
        
        var bookmark2 = new Bookmark 
        { 
            Id = Guid.NewGuid(), 
            Url = "https://learn.microsoft.com/csharp", 
            Title = "C# Documentation",
            DateAdded = DateTime.UtcNow,
            DateModified = DateTime.UtcNow
        };
        
        context.Categories.Add(techCategory);
        context.Tags.AddRange(csharpTag, dotnetTag);
        context.Bookmarks.AddRange(bookmark1, bookmark2);
        
        context.BookmarkTags.AddRange(
            new BookmarkTag { BookmarkId = bookmark1.Id, TagId = dotnetTag.Id, DateAssigned = DateTime.UtcNow },
            new BookmarkTag { BookmarkId = bookmark2.Id, TagId = csharpTag.Id, DateAssigned = DateTime.UtcNow },
            new BookmarkTag { BookmarkId = bookmark2.Id, TagId = dotnetTag.Id, DateAssigned = DateTime.UtcNow }
        );
        
        await context.SaveChangesAsync();
        
        // Test complex query: Get bookmarks with their tags
        var bookmarksWithTags = await context.Bookmarks
            .Include(b => b.BookmarkTags)
            .ThenInclude(bt => bt.Tag)
            .ToListAsync();
        
        bookmarksWithTags.Should().HaveCount(2);
        
        var dotnetBookmark = bookmarksWithTags.First(b => b.Title == ".NET Documentation");
        dotnetBookmark.BookmarkTags.Should().HaveCount(1);
        dotnetBookmark.BookmarkTags.First().Tag.Name.Should().Be(".NET");
        
        var csharpBookmark = bookmarksWithTags.First(b => b.Title == "C# Documentation");
        csharpBookmark.BookmarkTags.Should().HaveCount(2);
        csharpBookmark.BookmarkTags.Select(bt => bt.Tag.Name).Should().Contain(new[] { "C#", ".NET" });
    }

    private static async Task SeedDevelopmentDataAsync(BookmarkDbContext context)
    {
        // Check if already seeded
        if (await context.Bookmarks.AnyAsync())
        {
            return;
        }
        
        // Create categories
        var techCategory = new Category 
        { 
            Id = Guid.NewGuid(), 
            Name = "Technology", 
            Description = "Technology-related bookmarks",
            DateCreated = DateTime.UtcNow 
        };
        
        var newsCategory = new Category 
        { 
            Id = Guid.NewGuid(), 
            Name = "News", 
            Description = "News and current events",
            DateCreated = DateTime.UtcNow 
        };
        
        context.Categories.AddRange(techCategory, newsCategory);
        
        // Create tags
        var csharpTag = new Tag 
        { 
            Id = Guid.NewGuid(), 
            Name = "C#", 
            NormalizedName = "c#", 
            DateCreated = DateTime.UtcNow 
        };
        
        var dotnetTag = new Tag 
        { 
            Id = Guid.NewGuid(), 
            Name = ".NET", 
            NormalizedName = ".net", 
            DateCreated = DateTime.UtcNow 
        };
        
        var webdevTag = new Tag 
        { 
            Id = Guid.NewGuid(), 
            Name = "WebDev", 
            NormalizedName = "webdev", 
            DateCreated = DateTime.UtcNow 
        };
        
        context.Tags.AddRange(csharpTag, dotnetTag, webdevTag);
        
        // Create bookmarks
        var bookmark1 = new Bookmark 
        { 
            Id = Guid.NewGuid(), 
            Url = "https://docs.microsoft.com/dotnet", 
            Title = ".NET Documentation",
            Description = "Official Microsoft .NET documentation",
            DateAdded = DateTime.UtcNow,
            DateModified = DateTime.UtcNow,
            IsDeleted = false
        };
        
        var bookmark2 = new Bookmark 
        { 
            Id = Guid.NewGuid(), 
            Url = "https://learn.microsoft.com/aspnet/core", 
            Title = "ASP.NET Core Documentation",
            Description = "Learn ASP.NET Core web development",
            DateAdded = DateTime.UtcNow,
            DateModified = DateTime.UtcNow,
            IsDeleted = false
        };
        
        context.Bookmarks.AddRange(bookmark1, bookmark2);
        
        // Create bookmark-tag relationships
        context.BookmarkTags.AddRange(
            new BookmarkTag { BookmarkId = bookmark1.Id, TagId = dotnetTag.Id, DateAssigned = DateTime.UtcNow },
            new BookmarkTag { BookmarkId = bookmark2.Id, TagId = dotnetTag.Id, DateAssigned = DateTime.UtcNow },
            new BookmarkTag { BookmarkId = bookmark2.Id, TagId = webdevTag.Id, DateAssigned = DateTime.UtcNow }
        );
        
        await context.SaveChangesAsync();
    }
}
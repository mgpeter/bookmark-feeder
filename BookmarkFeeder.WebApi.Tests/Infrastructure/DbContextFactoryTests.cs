using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using BookmarkFeeder.WebApi.Data;
using BookmarkFeeder.WebApi.Models;

namespace BookmarkFeeder.WebApi.Tests.Infrastructure;

public class DbContextFactoryTests
{
    [Fact]
    public void DbContextFactory_ShouldBeRegisteredInDI()
    {
        var services = new ServiceCollection();
        
        services.AddDbContextFactory<BookmarkDbContext>(options =>
            options.UseInMemoryDatabase("FactoryRegistrationTest"));
        
        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetService<IDbContextFactory<BookmarkDbContext>>();
        
        factory.Should().NotBeNull();
    }

    [Fact]
    public void DbContextFactory_ShouldCreateMultipleIndependentContexts()
    {
        var services = new ServiceCollection();
        
        services.AddDbContextFactory<BookmarkDbContext>(options =>
            options.UseInMemoryDatabase("MultipleContextsTest"));
        
        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IDbContextFactory<BookmarkDbContext>>();
        
        using var context1 = factory.CreateDbContext();
        using var context2 = factory.CreateDbContext();
        
        context1.Should().NotBeNull();
        context2.Should().NotBeNull();
        context1.Should().NotBeSameAs(context2);
    }

    [Fact]
    public async Task DbContextFactory_CreatedContexts_ShouldHaveCorrectDbSets()
    {
        var services = new ServiceCollection();
        
        services.AddDbContextFactory<BookmarkDbContext>(options =>
            options.UseInMemoryDatabase("DbSetsTest"));
        
        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IDbContextFactory<BookmarkDbContext>>();
        
        using var context = factory.CreateDbContext();
        await context.Database.EnsureCreatedAsync();
        
        context.Bookmarks.Should().NotBeNull();
        context.Tags.Should().NotBeNull();
        context.Categories.Should().NotBeNull();
        context.BookmarkTags.Should().NotBeNull();
    }

    [Fact]
    public async Task DbContext_SoftDeleteFilter_ShouldFilterDeletedBookmarks()
    {
        var services = new ServiceCollection();
        
        services.AddDbContextFactory<BookmarkDbContext>(options =>
            options.UseInMemoryDatabase("SoftDeleteTest"));
        
        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IDbContextFactory<BookmarkDbContext>>();
        
        using var context = factory.CreateDbContext();
        await context.Database.EnsureCreatedAsync();
        
        // Add test data
        var activeBookmark = new Bookmark 
        { 
            Id = Guid.NewGuid(), 
            Url = "https://active.com", 
            Title = "Active Bookmark",
            DateAdded = DateTime.UtcNow,
            DateModified = DateTime.UtcNow,
            IsDeleted = false
        };
        
        var deletedBookmark = new Bookmark 
        { 
            Id = Guid.NewGuid(), 
            Url = "https://deleted.com", 
            Title = "Deleted Bookmark",
            DateAdded = DateTime.UtcNow,
            DateModified = DateTime.UtcNow,
            IsDeleted = true
        };
        
        context.Bookmarks.AddRange(activeBookmark, deletedBookmark);
        await context.SaveChangesAsync();
        
        // Query should only return non-deleted bookmarks
        var bookmarks = await context.Bookmarks.ToListAsync();
        bookmarks.Should().HaveCount(1);
        bookmarks[0].Should().Be(activeBookmark);
        bookmarks.Should().NotContain(deletedBookmark);
    }

    [Fact]
    public async Task DbContext_EntityConfigurations_ShouldHaveCorrectIndexes()
    {
        var services = new ServiceCollection();
        
        services.AddDbContextFactory<BookmarkDbContext>(options =>
            options.UseInMemoryDatabase("ConstraintsTest"));
        
        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IDbContextFactory<BookmarkDbContext>>();
        
        using var context = factory.CreateDbContext();
        await context.Database.EnsureCreatedAsync();
        
        // Test that we can add bookmarks with different URLs
        var bookmark1 = new Bookmark 
        { 
            Id = Guid.NewGuid(), 
            Url = "https://example1.com", 
            Title = "First Bookmark",
            DateAdded = DateTime.UtcNow,
            DateModified = DateTime.UtcNow
        };
        
        var bookmark2 = new Bookmark 
        { 
            Id = Guid.NewGuid(), 
            Url = "https://example2.com", 
            Title = "Second Bookmark",
            DateAdded = DateTime.UtcNow,
            DateModified = DateTime.UtcNow
        };
        
        context.Bookmarks.AddRange(bookmark1, bookmark2);
        await context.SaveChangesAsync();
        
        // Verify both bookmarks were saved
        var savedBookmarks = await context.Bookmarks.ToListAsync();
        savedBookmarks.Should().HaveCount(2);
        
        // Note: In-Memory database doesn't enforce unique constraints,
        // but the configuration is tested by the model creation
        var entityType = context.Model.FindEntityType(typeof(Bookmark));
        entityType.Should().NotBeNull();
        var urlIndex = entityType!.GetIndexes().FirstOrDefault(i => i.Properties.Any(p => p.Name == "Url"));
        urlIndex.Should().NotBeNull();
        urlIndex!.IsUnique.Should().BeTrue();
    }

    [Fact]
    public async Task DbContext_BookmarkTagRelationship_ShouldWorkCorrectly()
    {
        var services = new ServiceCollection();
        
        services.AddDbContextFactory<BookmarkDbContext>(options =>
            options.UseInMemoryDatabase("RelationshipTest"));
        
        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IDbContextFactory<BookmarkDbContext>>();
        
        using var context = factory.CreateDbContext();
        await context.Database.EnsureCreatedAsync();
        
        // Create test data
        var bookmark = new Bookmark 
        { 
            Id = Guid.NewGuid(), 
            Url = "https://test.com", 
            Title = "Test Bookmark",
            DateAdded = DateTime.UtcNow,
            DateModified = DateTime.UtcNow
        };
        
        var tag = new Tag 
        { 
            Id = Guid.NewGuid(), 
            Name = "Test Tag",
            NormalizedName = "test tag",
            DateCreated = DateTime.UtcNow
        };
        
        var bookmarkTag = new BookmarkTag 
        { 
            BookmarkId = bookmark.Id, 
            TagId = tag.Id,
            DateAssigned = DateTime.UtcNow
        };
        
        context.Bookmarks.Add(bookmark);
        context.Tags.Add(tag);
        context.BookmarkTags.Add(bookmarkTag);
        await context.SaveChangesAsync();
        
        // Verify relationship
        var savedBookmarkTag = await context.BookmarkTags
            .Include(bt => bt.Bookmark)
            .Include(bt => bt.Tag)
            .FirstAsync();
        
        savedBookmarkTag.Should().NotBeNull();
        savedBookmarkTag.BookmarkId.Should().Be(bookmark.Id);
        savedBookmarkTag.TagId.Should().Be(tag.Id);
        savedBookmarkTag.Bookmark.Should().NotBeNull();
        savedBookmarkTag.Tag.Should().NotBeNull();
    }

    [Fact]
    public async Task DbContext_CategoryHierarchy_ShouldWorkCorrectly()
    {
        var services = new ServiceCollection();
        
        services.AddDbContextFactory<BookmarkDbContext>(options =>
            options.UseInMemoryDatabase("CategoryHierarchyTest"));
        
        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IDbContextFactory<BookmarkDbContext>>();
        
        using var context = factory.CreateDbContext();
        await context.Database.EnsureCreatedAsync();
        
        // Create parent category
        var parentCategory = new Category 
        { 
            Id = Guid.NewGuid(), 
            Name = "Technology",
            DateCreated = DateTime.UtcNow
        };
        
        // Create child category
        var childCategory = new Category 
        { 
            Id = Guid.NewGuid(), 
            Name = "Programming",
            ParentCategoryId = parentCategory.Id,
            DateCreated = DateTime.UtcNow
        };
        
        context.Categories.AddRange(parentCategory, childCategory);
        await context.SaveChangesAsync();
        
        // Verify hierarchy
        var savedChildCategory = await context.Categories
            .Include(c => c.ParentCategory)
            .FirstAsync(c => c.Id == childCategory.Id);
        
        savedChildCategory.ParentCategory.Should().NotBeNull();
        savedChildCategory.ParentCategory!.Id.Should().Be(parentCategory.Id);
        savedChildCategory.ParentCategory.Name.Should().Be("Technology");
        
        var savedParentCategory = await context.Categories
            .Include(c => c.SubCategories)
            .FirstAsync(c => c.Id == parentCategory.Id);
        
        savedParentCategory.SubCategories.Should().HaveCount(1);
        savedParentCategory.SubCategories.First().Id.Should().Be(childCategory.Id);
    }

    [Fact]
    public async Task DbContext_TagNormalization_ShouldHaveUniqueIndex()
    {
        var services = new ServiceCollection();
        
        services.AddDbContextFactory<BookmarkDbContext>(options =>
            options.UseInMemoryDatabase("TagNormalizationTest"));
        
        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IDbContextFactory<BookmarkDbContext>>();
        
        using var context = factory.CreateDbContext();
        await context.Database.EnsureCreatedAsync();
        
        var tag1 = new Tag 
        { 
            Id = Guid.NewGuid(), 
            Name = "Technology",
            NormalizedName = "technology",
            DateCreated = DateTime.UtcNow
        };
        
        var tag2 = new Tag 
        { 
            Id = Guid.NewGuid(), 
            Name = "Programming",
            NormalizedName = "programming", // Different normalized name
            DateCreated = DateTime.UtcNow
        };
        
        context.Tags.AddRange(tag1, tag2);
        await context.SaveChangesAsync();
        
        // Verify both tags were saved
        var savedTags = await context.Tags.ToListAsync();
        savedTags.Should().HaveCount(2);
        
        // Verify unique index configuration on NormalizedName
        var entityType = context.Model.FindEntityType(typeof(Tag));
        entityType.Should().NotBeNull();
        var normalizedNameIndex = entityType!.GetIndexes().FirstOrDefault(i => i.Properties.Any(p => p.Name == "NormalizedName"));
        normalizedNameIndex.Should().NotBeNull();
        normalizedNameIndex!.IsUnique.Should().BeTrue();
    }
}
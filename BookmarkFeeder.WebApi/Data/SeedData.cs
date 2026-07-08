using BookmarkFeeder.WebApi.Models;
using Microsoft.EntityFrameworkCore;

namespace BookmarkFeeder.WebApi.Data;

public static class SeedData
{
    public static async Task SeedDevelopmentDataAsync(BookmarkDbContext context)
    {
        // Check if data already exists
        if (await context.Bookmarks.AnyAsync())
        {
            return; // Database has been seeded
        }

        // Create categories
        var techCategory = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Technology",
            Description = "Technology and programming resources",
            DateCreated = DateTime.UtcNow
        };

        var newsCategory = new Category
        {
            Id = Guid.NewGuid(),
            Name = "News",
            Description = "News and current events",
            DateCreated = DateTime.UtcNow
        };

        var programmingCategory = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Programming",
            ParentCategoryId = techCategory.Id,
            Description = "Programming languages and frameworks",
            DateCreated = DateTime.UtcNow
        };

        var webdevCategory = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Web Development",
            ParentCategoryId = programmingCategory.Id,
            Description = "Web development resources and tutorials",
            DateCreated = DateTime.UtcNow
        };

        context.Categories.AddRange(techCategory, newsCategory, programmingCategory, webdevCategory);

        // Create tags
        var csharpTag = new Tag
        {
            Id = Guid.NewGuid(),
            Name = "C#",
            NormalizedName = "c#",
            Color = "#9B4F96",
            DateCreated = DateTime.UtcNow
        };

        var dotnetTag = new Tag
        {
            Id = Guid.NewGuid(),
            Name = ".NET",
            NormalizedName = ".net",
            Color = "#512BD4",
            DateCreated = DateTime.UtcNow
        };

        var aspnetTag = new Tag
        {
            Id = Guid.NewGuid(),
            Name = "ASP.NET Core",
            NormalizedName = "asp.net core",
            DateCreated = DateTime.UtcNow
        };

        var webdevTag = new Tag
        {
            Id = Guid.NewGuid(),
            Name = "WebDev",
            NormalizedName = "webdev",
            DateCreated = DateTime.UtcNow
        };

        var documentationTag = new Tag
        {
            Id = Guid.NewGuid(),
            Name = "Documentation",
            NormalizedName = "documentation",
            DateCreated = DateTime.UtcNow
        };

        var tutorialTag = new Tag
        {
            Id = Guid.NewGuid(),
            Name = "Tutorial",
            NormalizedName = "tutorial",
            DateCreated = DateTime.UtcNow
        };

        var microsoftTag = new Tag
        {
            Id = Guid.NewGuid(),
            Name = "Microsoft",
            NormalizedName = "microsoft",
            DateCreated = DateTime.UtcNow
        };

        context.Tags.AddRange(csharpTag, dotnetTag, aspnetTag, webdevTag, documentationTag, tutorialTag, microsoftTag);

        // Create bookmarks
        var now = DateTime.UtcNow;

        var bookmark1 = new Bookmark
        {
            Id = Guid.NewGuid(),
            Url = "https://docs.microsoft.com/dotnet",
            Title = ".NET Documentation",
            Description = "Official Microsoft .NET documentation and guides",
            SourceFolder = "Bookmarks Bar/Technology",
            CategoryId = techCategory.Id,
            IsRead = true,
            DateAdded = now.AddDays(-30),
            DateModified = now.AddDays(-30),
            IsDeleted = false
        };

        var bookmark2 = new Bookmark
        {
            Id = Guid.NewGuid(),
            Url = "https://learn.microsoft.com/aspnet/core",
            Title = "ASP.NET Core Documentation",
            Description = "Learn how to build web apps and APIs with ASP.NET Core",
            SourceFolder = "Bookmarks Bar/Technology/Web Development",
            CategoryId = webdevCategory.Id,
            DateAdded = now.AddDays(-25),
            DateModified = now.AddDays(-25),
            IsDeleted = false
        };

        var bookmark3 = new Bookmark
        {
            Id = Guid.NewGuid(),
            Url = "https://learn.microsoft.com/csharp",
            Title = "C# Programming Guide",
            Description = "Learn C# programming language concepts and syntax",
            SourceFolder = "Bookmarks Bar/Technology/Programming",
            CategoryId = programmingCategory.Id,
            DateAdded = now.AddDays(-20),
            DateModified = now.AddDays(-20),
            IsDeleted = false
        };

        var bookmark4 = new Bookmark
        {
            Id = Guid.NewGuid(),
            Url = "https://github.com/dotnet/aspire",
            Title = ".NET Aspire",
            Description = "An opinionated, cloud ready stack for building observable, production ready, distributed applications",
            SourceFolder = "Bookmarks Bar/Technology",
            CategoryId = techCategory.Id,
            DateAdded = now.AddDays(-15),
            DateModified = now.AddDays(-15),
            IsDeleted = false
        };

        var bookmark5 = new Bookmark
        {
            Id = Guid.NewGuid(),
            Url = "https://learn.microsoft.com/ef/core",
            Title = "Entity Framework Core",
            Description = "Modern object-database mapper for .NET",
            DateAdded = now.AddDays(-10),
            DateModified = now.AddDays(-10),
            IsDeleted = false
        };

        var bookmark6 = new Bookmark
        {
            Id = Guid.NewGuid(),
            Url = "https://www.nuget.org",
            Title = "NuGet Gallery",
            Description = "The package manager for .NET",
            DateAdded = now.AddDays(-5),
            DateModified = now.AddDays(-5),
            IsDeleted = false
        };

        var bookmark7 = new Bookmark
        {
            Id = Guid.NewGuid(),
            Url = "https://dotnetfoundation.org",
            Title = ".NET Foundation",
            Description = "Independent organization to foster open development around the .NET ecosystem",
            DateAdded = now.AddDays(-3),
            DateModified = now.AddDays(-3),
            IsDeleted = false
        };

        var bookmark8 = new Bookmark
        {
            Id = Guid.NewGuid(),
            Url = "https://devblogs.microsoft.com/dotnet",
            Title = ".NET Blog",
            Description = "The official .NET blog with updates, announcements, and deep dives",
            DateAdded = now.AddDays(-1),
            DateModified = now.AddDays(-1),
            IsDeleted = false
        };

        context.Bookmarks.AddRange(bookmark1, bookmark2, bookmark3, bookmark4, bookmark5, bookmark6, bookmark7, bookmark8);

        // Create bookmark-tag relationships
        var bookmarkTags = new[]
        {
            // .NET Documentation
            new BookmarkTag { BookmarkId = bookmark1.Id, TagId = dotnetTag.Id, DateAssigned = now.AddDays(-30) },
            new BookmarkTag { BookmarkId = bookmark1.Id, TagId = documentationTag.Id, DateAssigned = now.AddDays(-30) },
            new BookmarkTag { BookmarkId = bookmark1.Id, TagId = microsoftTag.Id, DateAssigned = now.AddDays(-30) },

            // ASP.NET Core Documentation
            new BookmarkTag { BookmarkId = bookmark2.Id, TagId = aspnetTag.Id, DateAssigned = now.AddDays(-25) },
            new BookmarkTag { BookmarkId = bookmark2.Id, TagId = dotnetTag.Id, DateAssigned = now.AddDays(-25) },
            new BookmarkTag { BookmarkId = bookmark2.Id, TagId = webdevTag.Id, DateAssigned = now.AddDays(-25) },
            new BookmarkTag { BookmarkId = bookmark2.Id, TagId = documentationTag.Id, DateAssigned = now.AddDays(-25) },
            new BookmarkTag { BookmarkId = bookmark2.Id, TagId = microsoftTag.Id, DateAssigned = now.AddDays(-25) },

            // C# Programming Guide
            new BookmarkTag { BookmarkId = bookmark3.Id, TagId = csharpTag.Id, DateAssigned = now.AddDays(-20) },
            new BookmarkTag { BookmarkId = bookmark3.Id, TagId = dotnetTag.Id, DateAssigned = now.AddDays(-20) },
            new BookmarkTag { BookmarkId = bookmark3.Id, TagId = documentationTag.Id, DateAssigned = now.AddDays(-20) },
            new BookmarkTag { BookmarkId = bookmark3.Id, TagId = microsoftTag.Id, DateAssigned = now.AddDays(-20) },

            // .NET Aspire
            new BookmarkTag { BookmarkId = bookmark4.Id, TagId = dotnetTag.Id, DateAssigned = now.AddDays(-15) },
            new BookmarkTag { BookmarkId = bookmark4.Id, TagId = microsoftTag.Id, DateAssigned = now.AddDays(-15) },

            // Entity Framework Core
            new BookmarkTag { BookmarkId = bookmark5.Id, TagId = dotnetTag.Id, DateAssigned = now.AddDays(-10) },
            new BookmarkTag { BookmarkId = bookmark5.Id, TagId = csharpTag.Id, DateAssigned = now.AddDays(-10) },
            new BookmarkTag { BookmarkId = bookmark5.Id, TagId = documentationTag.Id, DateAssigned = now.AddDays(-10) },
            new BookmarkTag { BookmarkId = bookmark5.Id, TagId = microsoftTag.Id, DateAssigned = now.AddDays(-10) },

            // NuGet Gallery
            new BookmarkTag { BookmarkId = bookmark6.Id, TagId = dotnetTag.Id, DateAssigned = now.AddDays(-5) },
            new BookmarkTag { BookmarkId = bookmark6.Id, TagId = microsoftTag.Id, DateAssigned = now.AddDays(-5) },

            // .NET Foundation
            new BookmarkTag { BookmarkId = bookmark7.Id, TagId = dotnetTag.Id, DateAssigned = now.AddDays(-3) },

            // .NET Blog
            new BookmarkTag { BookmarkId = bookmark8.Id, TagId = dotnetTag.Id, DateAssigned = now.AddDays(-1) },
            new BookmarkTag { BookmarkId = bookmark8.Id, TagId = microsoftTag.Id, DateAssigned = now.AddDays(-1) }
        };

        context.BookmarkTags.AddRange(bookmarkTags);

        await context.SaveChangesAsync();
    }
}
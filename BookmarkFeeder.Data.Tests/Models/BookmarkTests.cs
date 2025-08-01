using BookmarkFeeder.Data.Models;
using Shouldly;
using Xunit;

namespace BookmarkFeeder.Data.Tests.Models;

public class BookmarkTests
{
    [Fact]
    public void Bookmark_Should_Initialize_With_Default_Values()
    {
        // Arrange & Act
        var bookmark = new Bookmark();

        // Assert
        bookmark.Id.ShouldBe(0);
        bookmark.Url.ShouldBeNull();
        bookmark.Title.ShouldBeNull();
        bookmark.Description.ShouldBeNull();
        bookmark.SourceFolder.ShouldBeNull();
        bookmark.CreatedAt.ShouldBe(default(DateTime));
        bookmark.UpdatedAt.ShouldBe(default(DateTime));
        bookmark.BookmarkTags.ShouldNotBeNull();
        bookmark.BookmarkTags.ShouldBeEmpty();
        bookmark.Tags.ShouldNotBeNull();
        bookmark.Tags.ShouldBeEmpty();
    }

    [Fact]
    public void Bookmark_Should_Allow_Setting_All_Properties()
    {
        // Arrange
        var url = "https://example.com";
        var title = "Example Site";
        var description = "A test bookmark";
        var sourceFolder = "Development";
        var createdAt = DateTime.UtcNow;
        var updatedAt = DateTime.UtcNow.AddMinutes(5);

        // Act
        var bookmark = new Bookmark
        {
            Id = 1,
            Url = url,
            Title = title,
            Description = description,
            SourceFolder = sourceFolder,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };

        // Assert
        bookmark.Id.ShouldBe(1);
        bookmark.Url.ShouldBe(url);
        bookmark.Title.ShouldBe(title);
        bookmark.Description.ShouldBe(description);
        bookmark.SourceFolder.ShouldBe(sourceFolder);
        bookmark.CreatedAt.ShouldBe(createdAt);
        bookmark.UpdatedAt.ShouldBe(updatedAt);
    }

    [Fact]
    public void Bookmark_Should_Allow_Null_Optional_Properties()
    {
        // Arrange & Act
        var bookmark = new Bookmark
        {
            Url = "https://example.com",
            Title = "Example",
            Description = null,
            SourceFolder = null
        };

        // Assert
        bookmark.Description.ShouldBeNull();
        bookmark.SourceFolder.ShouldBeNull();
    }

    [Fact]
    public void Bookmark_Navigation_Properties_Should_Be_Modifiable()
    {
        // Arrange
        var bookmark = new Bookmark();
        var tag = new Tag { Name = "test" };
        var bookmarkTag = new BookmarkTag { BookmarkId = 1, TagId = 1 };

        // Act
        bookmark.Tags.Add(tag);
        bookmark.BookmarkTags.Add(bookmarkTag);

        // Assert
        bookmark.Tags.ShouldContain(tag);
        bookmark.BookmarkTags.ShouldContain(bookmarkTag);
    }
}
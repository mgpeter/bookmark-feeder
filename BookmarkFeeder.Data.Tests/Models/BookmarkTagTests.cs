using BookmarkFeeder.Data.Models;
using Shouldly;
using Xunit;

namespace BookmarkFeeder.Data.Tests.Models;

public class BookmarkTagTests
{
    [Fact]
    public void BookmarkTag_Should_Initialize_With_Default_Values()
    {
        // Arrange & Act
        var bookmarkTag = new BookmarkTag();

        // Assert
        bookmarkTag.BookmarkId.ShouldBe(0);
        bookmarkTag.TagId.ShouldBe(0);
        bookmarkTag.CreatedAt.ShouldBe(default(DateTime));
        bookmarkTag.Bookmark.ShouldBeNull();
        bookmarkTag.Tag.ShouldBeNull();
    }

    [Fact]
    public void BookmarkTag_Should_Allow_Setting_All_Properties()
    {
        // Arrange
        var bookmarkId = 1;
        var tagId = 2;
        var createdAt = DateTime.UtcNow;
        var bookmark = new Bookmark { Id = bookmarkId, Url = "https://example.com", Title = "Test" };
        var tag = new Tag { Id = tagId, Name = "test", NormalizedName = "test" };

        // Act
        var bookmarkTag = new BookmarkTag
        {
            BookmarkId = bookmarkId,
            TagId = tagId,
            CreatedAt = createdAt,
            Bookmark = bookmark,
            Tag = tag
        };

        // Assert
        bookmarkTag.BookmarkId.ShouldBe(bookmarkId);
        bookmarkTag.TagId.ShouldBe(tagId);
        bookmarkTag.CreatedAt.ShouldBe(createdAt);
        bookmarkTag.Bookmark.ShouldBe(bookmark);
        bookmarkTag.Tag.ShouldBe(tag);
    }

    [Fact]
    public void BookmarkTag_Should_Represent_Many_To_Many_Relationship()
    {
        // Arrange
        var bookmark = new Bookmark { Id = 1, Url = "https://example.com", Title = "Test" };
        var tag = new Tag { Id = 1, Name = "test", NormalizedName = "test" };

        // Act
        var bookmarkTag = new BookmarkTag
        {
            BookmarkId = bookmark.Id,
            TagId = tag.Id,
            Bookmark = bookmark,
            Tag = tag,
            CreatedAt = DateTime.UtcNow
        };

        // Assert
        bookmarkTag.BookmarkId.ShouldBe(bookmark.Id);
        bookmarkTag.TagId.ShouldBe(tag.Id);
        bookmarkTag.Bookmark.ShouldBe(bookmark);
        bookmarkTag.Tag.ShouldBe(tag);
    }
}
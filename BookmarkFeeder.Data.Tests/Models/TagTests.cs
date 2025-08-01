using BookmarkFeeder.Data.Models;
using Shouldly;
using Xunit;

namespace BookmarkFeeder.Data.Tests.Models;

public class TagTests
{
    [Fact]
    public void Tag_Should_Initialize_With_Default_Values()
    {
        // Arrange & Act
        var tag = new Tag();

        // Assert
        tag.Id.ShouldBe(0);
        tag.Name.ShouldBeNull();
        tag.NormalizedName.ShouldBeNull();
        tag.CreatedAt.ShouldBe(default(DateTime));
        tag.BookmarkTags.ShouldNotBeNull();
        tag.BookmarkTags.ShouldBeEmpty();
        tag.Bookmarks.ShouldNotBeNull();
        tag.Bookmarks.ShouldBeEmpty();
    }

    [Fact]
    public void Tag_Should_Allow_Setting_All_Properties()
    {
        // Arrange
        var name = "JavaScript";
        var normalizedName = "javascript";
        var createdAt = DateTime.UtcNow;

        // Act
        var tag = new Tag
        {
            Id = 1,
            Name = name,
            NormalizedName = normalizedName,
            CreatedAt = createdAt
        };

        // Assert
        tag.Id.ShouldBe(1);
        tag.Name.ShouldBe(name);
        tag.NormalizedName.ShouldBe(normalizedName);
        tag.CreatedAt.ShouldBe(createdAt);
    }

    [Fact]
    public void Tag_Navigation_Properties_Should_Be_Modifiable()
    {
        // Arrange
        var tag = new Tag();
        var bookmark = new Bookmark { Url = "https://example.com", Title = "Test" };
        var bookmarkTag = new BookmarkTag { BookmarkId = 1, TagId = 1 };

        // Act
        tag.Bookmarks.Add(bookmark);
        tag.BookmarkTags.Add(bookmarkTag);

        // Assert
        tag.Bookmarks.ShouldContain(bookmark);
        tag.BookmarkTags.ShouldContain(bookmarkTag);
    }

    [Theory]
    [InlineData("JavaScript", "javascript")]
    [InlineData("WEB DEVELOPMENT", "web development")]
    [InlineData("C#", "c#")]
    [InlineData("  SpAcEs  ", "  spaces  ")]
    public void Tag_NormalizedName_Should_Store_Lowercase_Version(string name, string expectedNormalized)
    {
        // Arrange & Act
        var tag = new Tag
        {
            Name = name,
            NormalizedName = name.ToLowerInvariant()
        };

        // Assert
        tag.Name.ShouldBe(name);
        tag.NormalizedName.ShouldBe(expectedNormalized);
    }
}
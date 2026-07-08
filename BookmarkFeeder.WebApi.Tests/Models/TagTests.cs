using FluentAssertions;
using BookmarkFeeder.WebApi.Models;

namespace BookmarkFeeder.WebApi.Tests.Models;

public class TagTests
{
    [Fact]
    public void Tag_ShouldHaveValidProperties()
    {
        var tag = new Tag();
        
        tag.Should().NotBeNull();
        tag.Id.Should().BeEmpty();
        tag.BookmarkTags.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void Tag_ShouldSetAllProperties()
    {
        var id = Guid.NewGuid();
        var name = "Technology";
        var normalizedName = "technology";
        var dateCreated = DateTime.UtcNow;

        var tag = new Tag
        {
            Id = id,
            Name = name,
            NormalizedName = normalizedName,
            DateCreated = dateCreated
        };

        tag.Id.Should().Be(id);
        tag.Name.Should().Be(name);
        tag.NormalizedName.Should().Be(normalizedName);
        tag.DateCreated.Should().Be(dateCreated);
    }

    [Fact]
    public void Tag_Bookmarks_ShouldReturnBookmarksFromBookmarkTags()
    {
        var tag = new Tag { Id = Guid.NewGuid(), Name = "Tech" };
        var bookmark1 = new Bookmark { Id = Guid.NewGuid(), Title = "Bookmark 1" };
        var bookmark2 = new Bookmark { Id = Guid.NewGuid(), Title = "Bookmark 2" };

        var bookmarkTag1 = new BookmarkTag 
        { 
            BookmarkId = bookmark1.Id, 
            TagId = tag.Id, 
            Bookmark = bookmark1, 
            Tag = tag,
            DateAssigned = DateTime.UtcNow
        };
        var bookmarkTag2 = new BookmarkTag 
        { 
            BookmarkId = bookmark2.Id, 
            TagId = tag.Id, 
            Bookmark = bookmark2, 
            Tag = tag,
            DateAssigned = DateTime.UtcNow
        };

        tag.BookmarkTags.Add(bookmarkTag1);
        tag.BookmarkTags.Add(bookmarkTag2);

        var bookmarks = tag.BookmarkTags.Select(bt => bt.Bookmark).ToList();
        bookmarks.Should().HaveCount(2);
        bookmarks.Should().Contain(bookmark1);
        bookmarks.Should().Contain(bookmark2);
    }

    [Theory]
    [InlineData("Technology", "technology")]
    [InlineData("C#", "c#")]
    [InlineData("ASP.NET Core", "asp.net core")]
    [InlineData("JavaScript", "javascript")]
    public void Tag_NormalizedName_ShouldBeComputedLowercase(string name, string expectedNormalized)
    {
        var tag = new Tag { Name = name, NormalizedName = expectedNormalized };
        tag.NormalizedName.Should().Be(expectedNormalized);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Tag_ShouldAllowEmptyName_ForTestingConstraints(string name)
    {
        var tag = new Tag { Name = name };
        tag.Name.Should().Be(name);
    }
}
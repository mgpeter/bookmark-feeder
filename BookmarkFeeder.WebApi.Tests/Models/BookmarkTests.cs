using FluentAssertions;
using BookmarkFeeder.WebApi.Models;

namespace BookmarkFeeder.WebApi.Tests.Models;

public class BookmarkTests
{
    [Fact]
    public void Bookmark_ShouldHaveValidProperties()
    {
        var bookmark = new Bookmark();
        
        bookmark.Should().NotBeNull();
        bookmark.Id.Should().BeEmpty();
        bookmark.BookmarkTags.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void Bookmark_ShouldSetAllProperties()
    {
        var id = Guid.NewGuid();
        var url = "https://example.com";
        var title = "Example Title";
        var description = "Example Description";
        var dateAdded = DateTime.UtcNow;
        var dateModified = DateTime.UtcNow.AddMinutes(5);

        var bookmark = new Bookmark
        {
            Id = id,
            Url = url,
            Title = title,
            Description = description,
            DateAdded = dateAdded,
            DateModified = dateModified,
            IsDeleted = false
        };

        bookmark.Id.Should().Be(id);
        bookmark.Url.Should().Be(url);
        bookmark.Title.Should().Be(title);
        bookmark.Description.Should().Be(description);
        bookmark.DateAdded.Should().Be(dateAdded);
        bookmark.DateModified.Should().Be(dateModified);
        bookmark.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void Bookmark_Tags_ShouldReturnTagsFromBookmarkTags()
    {
        var bookmark = new Bookmark { Id = Guid.NewGuid() };
        var tag1 = new Tag { Id = Guid.NewGuid(), Name = "Tech" };
        var tag2 = new Tag { Id = Guid.NewGuid(), Name = "Programming" };

        var bookmarkTag1 = new BookmarkTag 
        { 
            BookmarkId = bookmark.Id, 
            TagId = tag1.Id, 
            Bookmark = bookmark, 
            Tag = tag1,
            DateAssigned = DateTime.UtcNow
        };
        var bookmarkTag2 = new BookmarkTag 
        { 
            BookmarkId = bookmark.Id, 
            TagId = tag2.Id, 
            Bookmark = bookmark, 
            Tag = tag2,
            DateAssigned = DateTime.UtcNow
        };

        bookmark.BookmarkTags.Add(bookmarkTag1);
        bookmark.BookmarkTags.Add(bookmarkTag2);

        var tags = bookmark.BookmarkTags.Select(bt => bt.Tag).ToList();
        tags.Should().HaveCount(2);
        tags.Should().Contain(tag1);
        tags.Should().Contain(tag2);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Bookmark_ShouldAllowEmptyUrl_ForTestingConstraints(string url)
    {
        var bookmark = new Bookmark { Url = url };
        bookmark.Url.Should().Be(url);
    }

    [Fact]
    public void Bookmark_ShouldHaveDefaultIsDeletedFalse()
    {
        var bookmark = new Bookmark();
        bookmark.IsDeleted.Should().BeFalse();
    }
}
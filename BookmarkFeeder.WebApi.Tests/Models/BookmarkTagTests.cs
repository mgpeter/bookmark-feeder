using FluentAssertions;
using BookmarkFeeder.WebApi.Models;

namespace BookmarkFeeder.WebApi.Tests.Models;

public class BookmarkTagTests
{
    [Fact]
    public void BookmarkTag_ShouldHaveValidProperties()
    {
        var bookmarkTag = new BookmarkTag();
        
        bookmarkTag.Should().NotBeNull();
        bookmarkTag.BookmarkId.Should().BeEmpty();
        bookmarkTag.TagId.Should().BeEmpty();
        bookmarkTag.DateAssigned.Should().Be(default(DateTime));
    }

    [Fact]
    public void BookmarkTag_ShouldSetAllProperties()
    {
        var bookmarkId = Guid.NewGuid();
        var tagId = Guid.NewGuid();
        var dateAssigned = DateTime.UtcNow;

        var bookmarkTag = new BookmarkTag
        {
            BookmarkId = bookmarkId,
            TagId = tagId,
            DateAssigned = dateAssigned
        };

        bookmarkTag.BookmarkId.Should().Be(bookmarkId);
        bookmarkTag.TagId.Should().Be(tagId);
        bookmarkTag.DateAssigned.Should().Be(dateAssigned);
    }

    [Fact]
    public void BookmarkTag_ShouldSupportNavigationProperties()
    {
        var bookmark = new Bookmark 
        { 
            Id = Guid.NewGuid(), 
            Url = "https://example.com", 
            Title = "Example"
        };
        
        var tag = new Tag 
        { 
            Id = Guid.NewGuid(), 
            Name = "Technology",
            NormalizedName = "technology"
        };

        var bookmarkTag = new BookmarkTag
        {
            BookmarkId = bookmark.Id,
            TagId = tag.Id,
            Bookmark = bookmark,
            Tag = tag,
            DateAssigned = DateTime.UtcNow
        };

        bookmarkTag.Bookmark.Should().Be(bookmark);
        bookmarkTag.Tag.Should().Be(tag);
        bookmarkTag.BookmarkId.Should().Be(bookmark.Id);
        bookmarkTag.TagId.Should().Be(tag.Id);
    }

    [Fact]
    public void BookmarkTag_ShouldSupportManyToManyRelationship()
    {
        var bookmark1 = new Bookmark { Id = Guid.NewGuid(), Title = "Bookmark 1" };
        var bookmark2 = new Bookmark { Id = Guid.NewGuid(), Title = "Bookmark 2" };
        var tag1 = new Tag { Id = Guid.NewGuid(), Name = "Tag 1" };
        var tag2 = new Tag { Id = Guid.NewGuid(), Name = "Tag 2" };

        // Bookmark1 has Tag1 and Tag2
        var bt1_t1 = new BookmarkTag 
        { 
            BookmarkId = bookmark1.Id, 
            TagId = tag1.Id, 
            Bookmark = bookmark1, 
            Tag = tag1,
            DateAssigned = DateTime.UtcNow
        };
        var bt1_t2 = new BookmarkTag 
        { 
            BookmarkId = bookmark1.Id, 
            TagId = tag2.Id, 
            Bookmark = bookmark1, 
            Tag = tag2,
            DateAssigned = DateTime.UtcNow
        };

        // Bookmark2 has Tag1
        var bt2_t1 = new BookmarkTag 
        { 
            BookmarkId = bookmark2.Id, 
            TagId = tag1.Id, 
            Bookmark = bookmark2, 
            Tag = tag1,
            DateAssigned = DateTime.UtcNow
        };

        // Set up relationships
        bookmark1.BookmarkTags.Add(bt1_t1);
        bookmark1.BookmarkTags.Add(bt1_t2);
        bookmark2.BookmarkTags.Add(bt2_t1);

        tag1.BookmarkTags.Add(bt1_t1);
        tag1.BookmarkTags.Add(bt2_t1);
        tag2.BookmarkTags.Add(bt1_t2);

        // Verify relationships through the BookmarkTags join navigation.
        bookmark1.BookmarkTags.Should().HaveCount(2);
        bookmark1.BookmarkTags.Select(bt => bt.Tag).Should().Contain(tag1);
        bookmark1.BookmarkTags.Select(bt => bt.Tag).Should().Contain(tag2);

        bookmark2.BookmarkTags.Should().HaveCount(1);
        bookmark2.BookmarkTags.Select(bt => bt.Tag).Should().Contain(tag1);
        bookmark2.BookmarkTags.Select(bt => bt.Tag).Should().NotContain(tag2);

        tag1.BookmarkTags.Should().HaveCount(2);
        tag1.BookmarkTags.Select(bt => bt.Bookmark).Should().Contain(bookmark1);
        tag1.BookmarkTags.Select(bt => bt.Bookmark).Should().Contain(bookmark2);

        tag2.BookmarkTags.Should().HaveCount(1);
        tag2.BookmarkTags.Select(bt => bt.Bookmark).Should().Contain(bookmark1);
        tag2.BookmarkTags.Select(bt => bt.Bookmark).Should().NotContain(bookmark2);
    }

    [Fact]
    public void BookmarkTag_ShouldTrackDateAssigned()
    {
        var dateAssigned = DateTime.UtcNow;
        var bookmarkTag = new BookmarkTag
        {
            BookmarkId = Guid.NewGuid(),
            TagId = Guid.NewGuid(),
            DateAssigned = dateAssigned
        };

        bookmarkTag.DateAssigned.Should().Be(dateAssigned);
        bookmarkTag.DateAssigned.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }
}
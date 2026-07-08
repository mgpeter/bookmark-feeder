using BookmarkFeeder.WebApi.Data;
using BookmarkFeeder.WebApi.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BookmarkFeeder.WebApi.Tests.Infrastructure;

public class ModelRegressionTests
{
    private static BookmarkDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<BookmarkDbContext>()
            .UseInMemoryDatabase($"model-{Guid.NewGuid()}")
            .Options;
        return new BookmarkDbContext(options);
    }

    [Fact]
    public void Model_HasSingleExplicitJoinTable_AndNoImplicitSkipNavigation()
    {
        using var context = CreateContext();
        var model = context.Model;

        // The Bookmark<->Tag relationship must go only through the explicit BookmarkTag entity.
        model.FindEntityType(typeof(Bookmark))!.GetSkipNavigations().Should().BeEmpty();
        model.FindEntityType(typeof(Tag))!.GetSkipNavigations().Should().BeEmpty();

        // No shadow / shared-type join entity (the previous bug created an implicit "BookmarkTag").
        model.GetEntityTypes().Should().NotContain(e => e.ClrType == null);
        model.GetEntityTypes().Count(e => e.ClrType == typeof(BookmarkTag)).Should().Be(1);
    }

    [Fact]
    public void Bookmark_HasCategoryForeignKey()
    {
        using var context = CreateContext();
        var bookmark = context.Model.FindEntityType(typeof(Bookmark))!;

        bookmark.GetForeignKeys()
            .Should().Contain(fk => fk.PrincipalEntityType.ClrType == typeof(Category));
    }
}

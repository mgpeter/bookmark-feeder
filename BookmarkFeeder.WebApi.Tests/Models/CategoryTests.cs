using FluentAssertions;
using BookmarkFeeder.WebApi.Models;

namespace BookmarkFeeder.WebApi.Tests.Models;

public class CategoryTests
{
    [Fact]
    public void Category_ShouldHaveValidProperties()
    {
        var category = new Category();
        
        category.Should().NotBeNull();
        category.Id.Should().BeEmpty();
        category.ParentCategoryId.Should().BeNull();
        category.ParentCategory.Should().BeNull();
        category.SubCategories.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void Category_ShouldSetAllProperties()
    {
        var id = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var name = "Technology";
        var description = "Tech-related bookmarks";
        var dateCreated = DateTime.UtcNow;

        var category = new Category
        {
            Id = id,
            Name = name,
            ParentCategoryId = parentId,
            Description = description,
            DateCreated = dateCreated
        };

        category.Id.Should().Be(id);
        category.Name.Should().Be(name);
        category.ParentCategoryId.Should().Be(parentId);
        category.Description.Should().Be(description);
        category.DateCreated.Should().Be(dateCreated);
    }

    [Fact]
    public void Category_ShouldSupportHierarchy()
    {
        var parentCategory = new Category 
        { 
            Id = Guid.NewGuid(), 
            Name = "Technology",
            DateCreated = DateTime.UtcNow
        };

        var childCategory1 = new Category 
        { 
            Id = Guid.NewGuid(), 
            Name = "Programming",
            ParentCategoryId = parentCategory.Id,
            ParentCategory = parentCategory,
            DateCreated = DateTime.UtcNow
        };

        var childCategory2 = new Category 
        { 
            Id = Guid.NewGuid(), 
            Name = "Web Development",
            ParentCategoryId = parentCategory.Id,
            ParentCategory = parentCategory,
            DateCreated = DateTime.UtcNow
        };

        parentCategory.SubCategories.Add(childCategory1);
        parentCategory.SubCategories.Add(childCategory2);

        // Test parent-child relationships
        parentCategory.SubCategories.Should().HaveCount(2);
        parentCategory.SubCategories.Should().Contain(childCategory1);
        parentCategory.SubCategories.Should().Contain(childCategory2);

        childCategory1.ParentCategory.Should().Be(parentCategory);
        childCategory1.ParentCategoryId.Should().Be(parentCategory.Id);
        
        childCategory2.ParentCategory.Should().Be(parentCategory);
        childCategory2.ParentCategoryId.Should().Be(parentCategory.Id);
    }

    [Fact]
    public void Category_ShouldSupportRootCategory()
    {
        var rootCategory = new Category 
        { 
            Id = Guid.NewGuid(), 
            Name = "Root",
            ParentCategoryId = null,
            DateCreated = DateTime.UtcNow
        };

        rootCategory.ParentCategoryId.Should().BeNull();
        rootCategory.ParentCategory.Should().BeNull();
        rootCategory.SubCategories.Should().BeEmpty();
    }

    [Fact]
    public void Category_ShouldSupportMultipleLevels()
    {
        // Root -> Technology -> Programming -> C#
        var rootCategory = new Category { Id = Guid.NewGuid(), Name = "Root" };
        var techCategory = new Category 
        { 
            Id = Guid.NewGuid(), 
            Name = "Technology", 
            ParentCategoryId = rootCategory.Id,
            ParentCategory = rootCategory
        };
        var programmingCategory = new Category 
        { 
            Id = Guid.NewGuid(), 
            Name = "Programming", 
            ParentCategoryId = techCategory.Id,
            ParentCategory = techCategory
        };
        var csharpCategory = new Category 
        { 
            Id = Guid.NewGuid(), 
            Name = "C#", 
            ParentCategoryId = programmingCategory.Id,
            ParentCategory = programmingCategory
        };

        // Build hierarchy
        rootCategory.SubCategories.Add(techCategory);
        techCategory.SubCategories.Add(programmingCategory);
        programmingCategory.SubCategories.Add(csharpCategory);

        // Verify hierarchy
        rootCategory.ParentCategoryId.Should().BeNull();
        techCategory.ParentCategoryId.Should().Be(rootCategory.Id);
        programmingCategory.ParentCategoryId.Should().Be(techCategory.Id);
        csharpCategory.ParentCategoryId.Should().Be(programmingCategory.Id);

        rootCategory.SubCategories.Should().Contain(techCategory);
        techCategory.SubCategories.Should().Contain(programmingCategory);
        programmingCategory.SubCategories.Should().Contain(csharpCategory);
        csharpCategory.SubCategories.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Category_ShouldAllowEmptyName_ForTestingConstraints(string name)
    {
        var category = new Category { Name = name };
        category.Name.Should().Be(name);
    }

    [Fact]
    public void Category_Description_ShouldBeOptional()
    {
        var category = new Category 
        { 
            Name = "Technology",
            Description = null
        };
        
        category.Description.Should().BeNull();
    }
}
namespace BookmarkFeeder.WebApi.Dtos;

public record CategoryDto(
    Guid Id,
    string Name,
    string? Description,
    Guid? ParentCategoryId,
    int Level,
    int BookmarkCount,
    DateTime DateCreated,
    List<CategoryDto> Children);

public record CreateCategoryRequest(string Name, string? Description, Guid? ParentCategoryId);

public record UpdateCategoryRequest(string Name, string? Description, Guid? ParentCategoryId);

namespace BookmarkFeeder.WebApi.Dtos;

public record TagDto(
    Guid Id,
    string Name,
    string NormalizedName,
    string? Color,
    DateTime DateCreated,
    int BookmarkCount);

public record CreateTagRequest(string Name, string? Color);

public record UpdateTagRequest(string Name, string? Color);

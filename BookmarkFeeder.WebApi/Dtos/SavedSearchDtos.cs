namespace BookmarkFeeder.WebApi.Dtos;

public record SavedSearchDto(Guid Id, string Name, string Query, DateTime DateCreated);

public record CreateSavedSearchRequest(string Name, string Query);

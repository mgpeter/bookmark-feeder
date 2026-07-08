namespace BookmarkFeeder.WebApi.Common;

public record PaginationDto(int Page, int PageSize, int TotalItems, int TotalPages);

public record PagedResult<T>(IReadOnlyList<T> Data, PaginationDto Pagination)
{
    public static PagedResult<T> Create(IReadOnlyList<T> data, int page, int pageSize, int totalItems)
    {
        var totalPages = pageSize <= 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);
        return new PagedResult<T>(data, new PaginationDto(page, pageSize, totalItems, totalPages));
    }
}

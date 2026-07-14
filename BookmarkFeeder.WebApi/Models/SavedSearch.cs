namespace BookmarkFeeder.WebApi.Models;

/// <summary>
/// A named search the user can re-run: the query and its filters, stored as the serialized
/// param string the UI uses. Single-tenant — there is no user to scope it to.
/// </summary>
public class SavedSearch
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The serialized query string, e.g. "q=graphql&amp;tags=dotnet&amp;sortBy=relevance".
    /// Stored opaquely: the UI owns its shape, so new filters need no schema change.
    /// </summary>
    public string Query { get; set; } = string.Empty;

    public DateTime DateCreated { get; set; }
}

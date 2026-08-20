namespace BookmarkFeeder.WebApi.Models;

/// <summary>
/// A named search the user can re-run: the query and its filters, stored as the serialized
/// param string the UI uses. Scoped to its owner like everything else.
/// </summary>
public class SavedSearch : IOwnedEntity
{
    public Guid Id { get; set; }

    /// <summary>The account this belongs to. Enforced by the "Owner" global query filter.</summary>
    public Guid OwnerId { get; set; }
    public User Owner { get; set; } = null!;


    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The serialized query string, e.g. "q=graphql&amp;tags=dotnet&amp;sortBy=relevance".
    /// Stored opaquely: the UI owns its shape, so new filters need no schema change.
    /// </summary>
    public string Query { get; set; } = string.Empty;

    public DateTime DateCreated { get; set; }
}

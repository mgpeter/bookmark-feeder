namespace BookmarkFeeder.WebApi.Models;

/// <summary>
/// An entity belonging to exactly one <see cref="User"/>. BookmarkDbContext stamps OwnerId on
/// insert for everything implementing this, so no create path has to remember to set it.
/// </summary>
public interface IOwnedEntity
{
    Guid OwnerId { get; set; }
}

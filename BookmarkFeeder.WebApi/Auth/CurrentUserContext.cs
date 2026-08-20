namespace BookmarkFeeder.WebApi.Auth;

/// <summary>
/// The owner every query in this request scope is scoped to. Read by BookmarkDbContext's "Owner"
/// global query filter, so it is the single point that makes the API multi-tenant.
/// </summary>
public interface ICurrentUserContext
{
    /// <summary>
    /// Guid.Empty when unresolved — a fail-CLOSED default. Empty matches no rows, so a scope with
    /// no authenticated user (health checks, the startup migration, the favicon worker) sees
    /// nothing rather than everything.
    /// </summary>
    Guid UserId { get; }

    bool IsResolved { get; }

    void Set(Guid userId);
}

public sealed class CurrentUserContext : ICurrentUserContext
{
    public Guid UserId { get; private set; } = Guid.Empty;

    public bool IsResolved => UserId != Guid.Empty;

    public void Set(Guid userId)
    {
        // Set once per scope. A second, different value would mean any DbContext already created
        // in this scope is filtering on a stale owner — a silent cross-tenant read.
        if (IsResolved && UserId != userId)
        {
            throw new InvalidOperationException("The current user is already resolved for this scope.");
        }

        UserId = userId;
    }
}

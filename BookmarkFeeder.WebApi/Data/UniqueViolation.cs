using Npgsql;

namespace BookmarkFeeder.WebApi.Data;

/// <summary>
/// The services detect duplicates (bookmark URLs, tag names) with a read before the save. Two
/// requests can pass that read together - two browsers syncing overlapping folders, or two
/// requests introducing the same new tag - and then the database rejects the loser's whole save:
/// a unique violation, or, when both are inserting into the same index, a deadlock. Rerunning the
/// operation with a fresh context gives the answer the read would have given: the winner's rows
/// now exist, so they are reported as duplicates or reused.
/// </summary>
public static class UniqueViolation
{
    private const int MaxAttempts = 3;

    /// <summary>True when the save lost a uniqueness race (duplicate key) to another request.</summary>
    public static bool Is(Exception ex) => SqlState(ex) == PostgresErrorCodes.UniqueViolation;

    /// <summary>
    /// Duplicate key, deadlock or serialization failure, anywhere in the chain: the Aspire Npgsql
    /// integration's retrying execution strategy wraps them in an InvalidOperationException.
    /// </summary>
    private static bool IsLostRace(Exception ex) => SqlState(ex) is
        PostgresErrorCodes.UniqueViolation or
        PostgresErrorCodes.DeadlockDetected or
        PostgresErrorCodes.SerializationFailure;

    private static string? SqlState(Exception? ex)
    {
        for (; ex is not null; ex = ex.InnerException)
        {
            if (ex is PostgresException pg) return pg.SqlState;
        }
        return null;
    }

    /// <summary>Runs <paramref name="operation"/>, rerunning it if it lost a uniqueness race.</summary>
    public static async Task<T> RetryLostRaceAsync<T>(Func<Task<T>> operation)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (attempt < MaxAttempts && IsLostRace(ex))
            {
                // A short, jittered pause so concurrent losers do not collide again in lockstep.
                await Task.Delay(Random.Shared.Next(20, 80) * attempt);
            }
        }
    }
}

using BookmarkFeeder.WebApi.Data;
using Microsoft.EntityFrameworkCore;

namespace BookmarkFeeder.WebApi.Services;

/// <summary>
/// Drains the favicon queue, discovering each bookmark's icon in the background.
/// Deliberately unhurried and unreliable-tolerant: nothing here may affect the API request
/// path, and a missing favicon is a cosmetic loss, not a failure.
/// </summary>
public class FaviconBackgroundService(
    IFaviconQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<FaviconBackgroundService> logger) : BackgroundService
{
    /// <summary>Concurrent site fetches. Low on purpose — we are a guest on other people's servers.</summary>
    private const int MaxConcurrency = 4;

    /// <summary>Breathing room between fetches, so a backfill doesn't machine-gun hosts.</summary>
    private static readonly TimeSpan PolitenessDelay = TimeSpan.FromMilliseconds(200);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Favicon worker started");

        try
        {
            // Runs after the host's startup migration, so the column is guaranteed to exist.
            await BackfillAsync(stoppingToken);

            await Parallel.ForEachAsync(
                queue.DequeueAllAsync(stoppingToken),
                new ParallelOptions { MaxDegreeOfParallelism = MaxConcurrency, CancellationToken = stoppingToken },
                async (bookmarkId, ct) => await ProcessAsync(bookmarkId, ct));
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
    }

    /// <summary>
    /// How long a "no icon found" result stands before it is tried again. A failed attempt stamps
    /// the bookmark like a success, and the attempt cannot tell a site with no icon from one that
    /// was briefly down, timed out, or was rate-limiting - so without a retry a passing outage
    /// cost that bookmark its favicon forever. Monthly is rare enough to stay polite to sites
    /// that really have none.
    /// </summary>
    public static readonly TimeSpan RetryNoIconAfter = TimeSpan.FromDays(30);

    /// <summary>
    /// Queues every bookmark never attempted, plus those whose attempt found nothing more than
    /// <see cref="RetryNoIconAfter"/> ago. `FaviconFetchedAt` is the state machine: a success or
    /// a failure both stamp it, so this picks up new bookmarks and anything dropped by a full
    /// queue, and re-checks a site with no icon only once the retry period has passed.
    /// </summary>
    private async Task BackfillAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<BookmarkDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync(ct);

            // Ids only, and the SoftDelete filter still drops deleted rows.
            //
            // IgnoreQueryFilters(["Owner"]) because this worker runs on its own scope with no HTTP
            // request, so there is no current user and the owner filter would match NOTHING -
            // favicon enrichment would silently stop for everyone. Enrichment is deliberately
            // cross-owner: it reads a URL and writes an icon, and touches nothing user-visible.
            var retryBefore = DateTime.UtcNow - RetryNoIconAfter;
            var pending = await context.Bookmarks
                .IgnoreQueryFilters(["Owner"])
                .Where(b => b.FaviconFetchedAt == null ||
                            (b.FaviconUrl == null && b.FaviconFetchedAt < retryBefore))
                .Select(b => b.Id)
                .ToListAsync(ct);

            if (pending.Count == 0) return;

            var queued = pending.Count(queue.TryEnqueue);

            if (queued < pending.Count)
            {
                // Not a failure: the leftovers still have a null stamp, so the next start retries.
                logger.LogInformation(
                    "Favicon backfill queued {Queued} of {Pending} bookmarks; the queue was full, the rest follow on the next start",
                    queued, pending.Count);
            }
            else
            {
                logger.LogInformation("Favicon backfill queued {Queued} bookmarks", queued);
            }
        }
        catch (Exception ex)
        {
            // A backfill that can't run is not a reason to take the worker down.
            logger.LogDebug(ex, "Favicon backfill failed");
        }
    }

    private async Task ProcessAsync(Guid bookmarkId, CancellationToken ct)
    {
        // Nothing may escape: Parallel.ForEachAsync would tear down the whole loop, and one
        // broken site would silently end favicon discovery for the rest of the process.
        try
        {
            using var scope = scopeFactory.CreateScope();
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<BookmarkDbContext>>();
            var resolver = scope.ServiceProvider.GetRequiredService<IFaviconResolver>();

            await using var context = await contextFactory.CreateDbContextAsync(ct);

            // The SoftDelete filter excludes soft-deleted bookmarks, so a deleted one is simply
            // not found and never fetched. Owner is dropped for the same reason as the backfill:
            // no HTTP request means no current user, and the filter would match nothing.
            var bookmark = await context.Bookmarks
                .IgnoreQueryFilters(["Owner"])
                .FirstOrDefaultAsync(b => b.Id == bookmarkId, ct);
            if (bookmark is null) return;

            // The user (or an earlier run) already set one: keep it rather than overwrite a
            // deliberate choice with whatever discovery finds, and skip the outbound requests.
            if (!string.IsNullOrEmpty(bookmark.FaviconUrl))
            {
                bookmark.FaviconFetchedAt = DateTime.UtcNow;
                await context.SaveChangesAsync(ct);
                return;
            }

            string? favicon = null;
            try
            {
                favicon = await resolver.ResolveAsync(bookmark.Url, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A resolver failure is just "no favicon". Handled here rather than in the outer
                // catch so the attempt is still stamped below, through the one save path.
                logger.LogDebug(ex, "Favicon discovery threw for {Url}", bookmark.Url);
            }

            // Stamp either way. On failure FaviconUrl stays null, and the stamp is what stops
            // the backfill retrying this site on every startup.
            bookmark.FaviconUrl = favicon;
            bookmark.FaviconFetchedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(ct);

            await Task.Delay(PolitenessDelay, ct);
        }
        catch (OperationCanceledException)
        {
            // Shutdown mid-flight; the bookmark keeps a null stamp and is retried next start.
        }
        catch (Exception ex)
        {
            // Database trouble. Nothing to stamp with — the backfill will find it again.
            logger.LogDebug(ex, "Favicon processing failed for bookmark {BookmarkId}", bookmarkId);
        }
    }
}

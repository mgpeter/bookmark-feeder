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
    /// Queues every bookmark never attempted before. `FaviconFetchedAt == null` is the whole
    /// state machine: a success or a failure both stamp it, so this picks up new bookmarks and
    /// anything dropped by a full queue, but never re-fetches a site already known to have no
    /// discoverable icon.
    /// </summary>
    private async Task BackfillAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<BookmarkDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync(ct);

            // Ids only, and the query filter drops soft-deleted rows.
            var pending = await context.Bookmarks
                .Where(b => b.FaviconFetchedAt == null)
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

            // The global query filter excludes soft-deleted bookmarks, so a deleted one is
            // simply not found and never fetched.
            var bookmark = await context.Bookmarks.FirstOrDefaultAsync(b => b.Id == bookmarkId, ct);
            if (bookmark is null) return;

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
            bookmark.FaviconUrl = favicon ?? bookmark.FaviconUrl;
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

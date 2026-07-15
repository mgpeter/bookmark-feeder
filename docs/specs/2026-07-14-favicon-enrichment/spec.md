# Spec Requirements Document

> Spec: Favicon Enrichment
> Created: 2026-07-14
> Status: Completed
> Completed: 2026-07-14

## Overview

Populate `Bookmark.FaviconUrl` automatically via a background service that, for each new/synced
bookmark and as a one-time backfill of existing ones, discovers the site's favicon from its own
origin (parsing `<link rel="icon">` with a `/favicon.ico` fallback), validates it, and stores the
resolved remote URL - with rate-limited fetching and graceful failure. The web UI already renders
`FaviconUrl` (with a monogram fallback), so this is a backend-only enhancement.

## User Stories

### Recognisable bookmarks at a glance

As a user browsing my collection, I want each bookmark to show its site's real favicon instead of
a letter monogram, so that I can recognise and scan bookmarks faster.

When a bookmark is created or synced, a background worker discovers its favicon from the site and
fills in `FaviconUrl`; the card's avatar then shows the real icon. Existing bookmarks are filled in
over time by a one-time backfill.

### No surprises, no hammering

As a self-hoster, I want favicon fetching to be resilient and polite, so that a slow or missing
site never breaks anything and my server doesn't hammer external hosts.

Fetches only touch the bookmarked site's own origin (never a third-party favicon service), are
rate-limited, time out quickly, and fail silently (the monogram simply remains). A failed attempt
is recorded so it isn't retried endlessly.

## Spec Scope

1. **Background favicon worker** - The project's first `IHostedService` background worker + an
   in-memory channel queue that processes bookmark IDs needing a favicon.
2. **Favicon resolver** - Server-side discovery from the site's own origin: parse the page's
   `<link rel="icon">`/`apple-touch-icon`, fall back to `<origin>/favicon.ico`, validate it's an
   image, and resolve to an absolute URL.
3. **Enqueue + backfill** - Enqueue on bookmark create and batch sync; a one-time startup backfill
   enqueues existing bookmarks that have no favicon yet.
4. **Rate-limited, graceful fetching** - Bounded concurrency + short timeouts; failures are caught
   and recorded (so they aren't retried forever) without affecting the request path.

## Out of Scope

- Caching favicon **bytes** or serving them from our own API (we store the discovered remote URL;
  the browser fetches the image on render).
- Any third-party favicon service/proxy.
- Periodic re-refresh / expiry of favicons, and a manual per-bookmark refresh endpoint (possible
  later enhancement).
- Frontend changes - `FaviconAvatar` already consumes `FaviconUrl` with a monogram fallback.

## Expected Deliverable

1. A newly created or synced bookmark gets `FaviconUrl` populated shortly after (in the background),
   discovered from the site (via `<link rel=icon>` or `/favicon.ico`), and its card shows the real
   favicon.
2. Existing bookmarks without a favicon are backfilled; sites with no discoverable favicon fail
   gracefully (monogram remains) and are not retried repeatedly.
3. Fetching is rate-limited and only ever requests the bookmarked site's own origin.

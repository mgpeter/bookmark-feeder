# Spec Summary (Lite)

Populate `Bookmark.FaviconUrl` automatically with a background worker (the project's first
`IHostedService` + channel queue) that discovers each site's favicon from its own origin - parsing
`<link rel="icon">` with a `/favicon.ico` fallback - validates it, and stores the resolved remote
URL. It enqueues on create/sync and does a one-time backfill of existing bookmarks, with
rate-limited fetching and graceful failure (attempts recorded so they aren't retried endlessly).
Backend-only: the web UI already renders `FaviconUrl` with a monogram fallback.

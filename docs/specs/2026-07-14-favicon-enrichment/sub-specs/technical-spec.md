# Technical Specification

This is the technical specification for the spec detailed in @docs/specs/2026-07-14-favicon-enrichment/spec.md

## Technical Requirements

### 1. Model + schema (BookmarkFeeder.WebApi)

- `Bookmark.FaviconUrl` already exists (string, max 2048) — reused as the resolved remote URL.
- Add `Bookmark.FaviconFetchedAt` (`DateTime?`) to record when discovery was last attempted
  (success or failure), so backfill only picks up bookmarks where it is null and failures aren't
  retried forever. Configure in `Data/BookmarkDbContext.cs`; one EF migration adds the column.

### 2. Background worker + queue (the reusable job foundation)

- `IFaviconQueue` wrapping a bounded `System.Threading.Channels.Channel<Guid>` (bookmark IDs),
  registered as a singleton; `Enqueue(Guid id)` / `DequeueAllAsync`.
- `FaviconBackgroundService : BackgroundService` reads the queue and processes IDs with **bounded
  concurrency** (e.g. `SemaphoreSlim(4)`) plus a small politeness delay. This is the project's
  first hosted background service — the later AI job queue reuses this pattern.
- Register in `Program.cs`: `AddSingleton<IFaviconQueue>` + `AddHostedService<FaviconBackgroundService>`
  (+ the resolver and a named HttpClient). The worker resolves scoped services via
  `IServiceScopeFactory` / `IDbContextFactory<BookmarkDbContext>`.

### 3. Favicon resolver (`IFaviconResolver` / `FaviconResolver`)

- Uses `IHttpClientFactory` named client `"favicon"`: short timeout (~10s), redirects allowed,
  a bounded `MaxResponseContentBufferSize`, and a normal browser `User-Agent`.
- Discovery for a bookmark URL:
  1. GET the page HTML; parse `<link rel="icon">` / `rel="shortcut icon"` / `apple-touch-icon`
     with **AngleSharp**; pick the best (prefer larger `sizes`), resolve relative → absolute.
  2. Fallback: `HEAD`/`GET` `<origin>/favicon.ico`.
  3. Validate the chosen URL responds OK with an `image/*` content type.
  4. Return the absolute URL, or `null` if nothing usable.
- **Origin-only:** every request targets the bookmarked site's own scheme+host; never a
  third-party favicon service.

### 4. Enqueue + backfill

- **On create/sync:** after a successful save in `BookmarkService.CreateAsync` and
  `CreateBatchAsync` (`Services/BookmarkService.cs`), enqueue the new bookmark IDs (don't block the
  request; enqueue after `SaveChangesAsync`).
- **Backfill:** on startup the worker enqueues all non-deleted bookmarks with `FaviconFetchedAt == null`
  (batched), so existing rows fill in over time.

### 5. Processing + failure handling

- Per ID: load the (non-deleted) bookmark via `IDbContextFactory`; run the resolver; on success set
  `FaviconUrl` + `FaviconFetchedAt`; on failure set only `FaviconFetchedAt` (leaving `FaviconUrl`
  null) so it isn't retried. All exceptions are caught and logged at debug/info — favicon fetching
  never affects the API request path.

### 6. Frontend

- None. `FaviconAvatar` (`BookmarkFeeder.Web/src/components/favicon-avatar.tsx`) already prefers
  `faviconUrl` and falls back to a monogram on error, so populated favicons appear automatically.

### 7. Testing

- Unit-test `FaviconResolver` with a stub `HttpMessageHandler`: parses `<link rel=icon>` from sample
  HTML → correct absolute URL; `/favicon.ico` fallback when no `<link>`; returns `null` on
  404/non-image/timeout.
- Integration-test the enqueue→process flow with a fake resolver: enqueuing a bookmark id results in
  `FaviconUrl`/`FaviconFetchedAt` being set. (Real HTTP is not hit in tests.)

## External Dependencies

- **AngleSharp** - Robust, spec-compliant HTML parsing to extract `<link rel="icon">` from pages.
  - **Justification:** reliable icon discovery across real-world markup (regex over HTML is
    fragile); AngleSharp is the standard .NET HTML parser. Pin the latest stable at implementation.

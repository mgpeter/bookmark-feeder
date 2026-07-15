# Technical Specification

This is the technical specification for the spec detailed in @docs/specs/2026-07-14-mark-all-as-read/spec.md

## Technical Requirements

### Shared filter composition (the load-bearing change)

- Extract the filter chain currently inlined in `BookmarkService.GetBookmarksAsync` (search UNION,
  `tags`, `categories`, `sourceFolder`, `isRead`, `dateFrom`, `dateTo`) into a single private
  helper, e.g.
  `private static IQueryable<Bookmark> ApplyFilters(BookmarkDbContext context, IQueryable<Bookmark> q, BookmarkQuery query, string? searchTerm)`.
- `GetBookmarksAsync` and the new `MarkAllReadAsync` must both call it. This is the point of the
  refactor: if the two composed filters independently, the set marked read could drift from the set
  displayed, which is exactly the bug this feature can't have.
- The extraction is behaviour-preserving - the existing search tests must pass unchanged, including
  the UNION-of-ids approach that keeps the GIN index in play (see the comment in `BookmarkService`;
  do not collapse it back into an `OR`).

### Service: `MarkAllReadAsync`

- Signature: `Task<int> MarkAllReadAsync(BookmarkQuery query, bool isRead, CancellationToken ct = default)`
  on `IBookmarkService`.
- Build the filtered query **without** `Include`s - `ExecuteUpdateAsync` cannot be used with
  `Include`, and the navigations aren't needed.
- Ignore `Page`, `PageSize`, `SortBy`, `SortOrder`. The action deliberately spans all pages, and
  sorting an update is meaningless.
- Add `.Where(b => b.IsRead != isRead)` so rows already in the target state aren't touched and their
  `DateModified` isn't churned. The returned count is therefore rows *actually changed*, which may
  be lower than the matched total shown in the dialog - see "Count semantics" below.
- Apply via a single `ExecuteUpdateAsync` setting both `IsRead` and `DateModified`:

  ```csharp
  var now = DateTime.UtcNow;
  return await q.ExecuteUpdateAsync(
      s => s.SetProperty(b => b.IsRead, isRead)
            .SetProperty(b => b.DateModified, now),
      ct);
  ```

- One round-trip, no entity materialisation - the whole set updates in one statement regardless of
  size, which is why no job queue or progress UI is needed.
- The soft-delete global query filter applies to `ExecuteUpdateAsync` automatically, so
  soft-deleted bookmarks are excluded without extra code.

### Endpoint

- `POST /api/bookmarks/mark-read`, mapped in `BookmarkEndpoints.MapBookmarkEndpoints`.
- Binds `[AsParameters] BookmarkQuery` from the **query string** - the same binding the `GET`
  uses - so the client sends the identical filter string for both, and the two cannot disagree.
- Binds the existing `MarkReadRequest` (`{ isRead }`) from the **body** for the target state. The
  target must not live in the query string: `BookmarkQuery.IsRead` is already the read-state
  *filter*, and reusing that name for the target would be ambiguous. Filter in query string, target
  in body keeps them distinct and reuses both existing DTOs.
- Returns `200 OK` with `{ "updated": <int> }`.
- `.RequireRateLimiting("writes")` - consistent with the other mutating endpoints.
- Auth is inherited from the `/api` group's `X-API-Key` filter; no change.

### Count semantics (intentional, and worth not "fixing")

- The dialog's count comes from the list response's `pagination.totalItems` the user is already
  looking at - no extra count request.
- The toast's count is the endpoint's `updated`, i.e. rows that actually changed state.
- These differ when some matches are already read (e.g. dialog "137 matching", toast "updated 112").
  This is correct: the dialog describes the set, the toast describes the change.

### Frontend

- `src/api/bookmarks.ts`: add a `useMarkAllRead()` mutation posting to
  `/api/bookmarks/mark-read?<serialized filters>` with body `{ isRead: true }`, reusing whatever
  helper already serialises `BookmarkQuery` for the `GET` (do not hand-roll a second serialiser).
  Strip `page`/`pageSize`/`sortBy`/`sortOrder` before sending.
- On success: invalidate the bookmarks query key (and the dashboard counts key, which shows read
  totals), then `toast.success(...)` via the existing `sonner` setup.
- On error: `toast.error(...)`; the dialog closes either way.
- `bookmark-list-page.tsx`: a `variant="outline"` button with the `CheckCheck` icon (`lucide-react`)
  left of "Add bookmark", disabled when `pagination.totalItems === 0`, while loading, or while the
  mutation is pending.
- New `mark-all-read-dialog.tsx` in `src/features/bookmarks/`, built on the existing
  `components/ui/alert-dialog.tsx`. Props: the affected count, whether any filter is active, open
  state, and confirm/cancel handlers.
- Dialog copy driven by whether any filter is active - derive this from the `BookmarkQuery` fields
  that actually narrow the set (`search`, `tags`, `categories`, `sourceFolder`, `isRead`,
  `dateFrom`, `dateTo`), ignoring paging/sort:
  - Filtered: title "Mark all N matching bookmarks as read?", body noting it applies to every match
    across all pages, not just the current page.
  - Unfiltered: title "Mark all N bookmarks as read?", body "This affects your entire collection."
  - Confirm button "Mark as read"; cancel is the default `AlertDialogCancel`.

### Testing

- Backend (`BookmarkFeeder.WebApi.Tests`, xUnit + FluentAssertions + `WebApplicationFactory`):
  bulk-read tests alongside the existing search tests, covering filter parity with `GET`,
  all-pages behaviour, the already-read no-op, soft-delete exclusion, and auth.
- Frontend (Vitest + React Testing Library): dialog copy for the filtered vs unfiltered case, that
  cancel fires no request, and that confirm posts the current filters.

## External Dependencies

None. `alert-dialog.tsx` (Radix) and `sonner` are already in `components/ui/`, and the endpoint
reuses the existing `BookmarkQuery` and `MarkReadRequest` DTOs.

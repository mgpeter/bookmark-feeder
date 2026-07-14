# Spec Tasks

- [x] 1. Shared filter composition (behaviour-preserving refactor)
  - [x] 1.1 Baseline established before touching anything: 88/88 green (17 search tests + filter/pagination coverage)
  - [x] 1.2 Filter chain extracted verbatim into `ApplyFilters(context, q, query, searchTerm)` — search UNION-of-ids preserved, with the comment explaining why it must not become an `OR`. XML doc states the shared-use contract so a future filter added for the list can't silently skip the bulk path
  - [x] 1.3 88/88 pass with **zero test-file edits** (`git status` on the test project is empty) — the refactor changed no behaviour

- [x] 2. Bulk mark-read — service + endpoint
  - [x] 2.1 `Tests/Infrastructure/MarkAllReadTests.cs` (9 tests): all-pages (pageSize ignored), set parity with `GET` for the same filter string, already-read skipped (`updated` = 1 of 2 matched), soft-deleted excluded, `?isRead=false` + body `{isRead:true}`, bulk mark-*unread*, `DateModified` bumped, no filters ⇒ whole collection, `401` without key
  - [x] 2.2 `MarkAllReadAsync` — reuses `ApplyFilters` (no `Include`s), ignores page/sort, `.Where(b => b.IsRead != isRead)`, single `ExecuteUpdateAsync` setting `IsRead` + `DateModified`. `ExecuteUpdateAsync` composes correctly with the search UNION-of-ids subquery on real PostgreSQL (covered by the all-pages test, which filters by `search=`)
  - [x] 2.3 `POST /api/bookmarks/mark-read` — `[AsParameters] BookmarkQuery` (query string) + `MarkReadRequest` (body) bind together as specified; returns `{ updated }`; `writes` rate limit; auth inherited from the `/api` group
  - [x] 2.4 97/97 backend tests pass (88 + 9 new)

- [x] 3. Frontend — action, dialog, feedback
  - [x] 3.1 18 new tests across three files: `lib/bookmark-filters.test.ts` (narrowing vs paging/sort, `isRead:false` kept), `features/bookmarks/mark-all-read-dialog.test.tsx` (filtered vs whole-collection copy, count formatting, confirm/cancel), `api/bookmarks.test.tsx` (posts `search=` not `q=`, strips page/sort, body carries the target, returns `updated`)
  - [x] 3.2 `api.post` now takes optional params, reusing the existing `buildUrl` serialiser; `lib/bookmark-filters.ts` exposes `narrowingFilters`/`hasNarrowingFilters`; `useMarkAllRead()` strips paging/sort and invalidates the `bookmarks` key (which the dashboard shares)
  - [x] 3.3 `mark-all-read-dialog.tsx` on the existing `alert-dialog`; copy driven by `isFiltered`, counts via `toLocaleString()`, both variants state that it cannot be undone
  - [x] 3.4 Outline `CheckCheck` button left of "Add bookmark", disabled while loading / empty / pending; toast reports `updated`, with a distinct message when nothing needed changing
  - [x] 3.5 27/27 web tests pass (9 + 18 new); `npm run build` green

  **Note:** the tests initially used `sortBy: 'relevance'` and the build caught that `SortBy` doesn't include it — the backend gained relevance in full-text-search Task 2, but the frontend type is that spec's Task 5.2. Tests changed to `'title'` rather than widening the type here; the point of those assertions is that sort is stripped, whatever its value.

## Notes

- **No new dependencies.** `alert-dialog.tsx` and `sonner` are already in `components/ui/`; the endpoint reuses `BookmarkQuery` and `MarkReadRequest`.
- **Spec correction — the API filter param is `search`, not `q`.** `api-spec.md`'s example (`?q=graphql`) uses the *web app's* URL param name. The web app maps `q` → `BookmarkQuery.search` in `use-bookmark-query.ts`, and `api.get('/bookmarks', query)` serialises the object's own keys, so the API sees `search=`.
- **Spec correction — there is no separate dashboard counts key.** `dashboard-page.tsx` calls `useBookmarks`, so it shares the `bookmarks` query key; invalidating that one key refreshes both list and dashboard.
- **Count semantics are intentional:** the dialog reports matched rows (`pagination.totalItems`), the toast reports rows actually changed (`updated`). They differ when some matches were already read. Not a bug.

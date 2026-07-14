# Spec Tasks

- [x] 1. Generated `tsvector` column + GIN index
  - [x] 1.1 `Tests/Infrastructure/SearchVectorTests.cs` (6 Testcontainers tests): populates on insert, A/B/C weights, recomputes on title change, URL words indexed individually, column is `is_generated = ALWAYS`, GIN index exists
  - [x] 1.2 `SearchVector` (`NpgsqlTsVector`) added to `Models/Bookmark.cs`; not on `BookmarkDto`
  - [x] 1.3 **Spec's API assumption was wrong**: `HasGeneratedTsVectorColumn` has a single overload `(builder, vector, config, include)` with **no weights parameter** — it emits an unweighted `to_tsvector`, and `SetWeight` is query-side only. Used `HasComputedColumnSql(..., stored: true)` with the explicit `setweight` SQL from database-schema.md instead. Guarded by `Database.IsNpgsql()` — the InMemory provider can't map `tsvector` and fails model validation otherwise
  - [x] 1.4 Migration `20260714133222_AddBookmarkSearchVector` — stored generated column + GIN index. Applied to the dev database on restart: **434/434 rows populated, 0 lost** (a stored generated column backfills itself — no data migration step). Verified on real data: `EXPLAIN` shows `Bitmap Index Scan on IX_Bookmarks_SearchVector` (index used, not a seq scan); "github" ranks title hits at 0.7599 vs url-only at 0.1216 across 11 + 36 matches, confirming the weights order results
  - [x] 1.5 72/72 backend tests pass (66 pre-existing + 6 new)

  **Deviation from spec, deliberate:** the `Url` is normalized (`regexp_replace(Url, '[^a-zA-Z0-9]+', ' ', 'g')`) before `to_tsvector`. Postgres' parser indexes a URL as host/url_path tokens (`wolverine.netlify.app`, `/guide`), so a search for `wolverine` would **not** match `https://wolverine.netlify.app` — a regression against the `ILIKE '%term%'` search being replaced. Splitting to words keeps each segment matchable. Pinned by `SearchVector_IndexesUrlWordsIndividually_NotAsHostTokens`.

- [ ] 2. Ranked search in `GetBookmarksAsync`
  - [ ] 2.1 Write tests: matches title/description/url; matches tag names; ranks title matches above url matches; websearch syntax (`"quoted"`, `OR`, `-negated`); composes with tag/category/read/folder/date filters + pagination; malformed query doesn't 500
  - [ ] 2.2 Replace the `ILike` block with `WebSearchToTsQuery` + `SearchVector.Matches(...)` OR tag-name match — **verify the Npgsql extension names** (`Matches`, `Rank`, `WebSearchToTsQuery`)
  - [ ] 2.3 Add `relevance` to `ApplySort`; default to it when `search` is present and `sortBy` is unset, without breaking the existing default (`dateAdded desc`)
  - [ ] 2.4 Verify tests pass and existing bookmark tests stay green

- [ ] 3. Facets in the list response
  - [ ] 3.1 Write tests: tag/category counts reflect the current search+filters, count the whole result set (not just the page), and are absent/empty when nothing is active
  - [ ] 3.2 Compute tag + category facets over the filtered `IQueryable` before `Skip`/`Take`
  - [ ] 3.3 Add `facets` additively to the response so existing consumers are unaffected
  - [ ] 3.4 Verify tests pass

- [ ] 4. Saved searches — entity, service, endpoints
  - [ ] 4.1 Write tests: create → list → delete round-trip; `400` on empty name/query; `404` deleting an unknown id; `401` without the API key
  - [ ] 4.2 `Models/SavedSearch.cs` + `IEntityTypeConfiguration` (explicit column types) + migration for `SavedSearches`
  - [ ] 4.3 `ISavedSearchService`/`SavedSearchService` via `IDbContextFactory` (no generic repository)
  - [ ] 4.4 `SavedSearchEndpoints` mapped at `/api/searches` inside the `/api` group (API-key filter + reads/writes rate limits) + FluentValidation validator
  - [ ] 4.5 Verify tests pass

- [ ] 5. Frontend — relevance, highlighting, facets, saved searches
  - [ ] 5.1 Write component tests: highlight helper (incl. regex-unsafe terms), relevance defaults when `q` is present, clicking a facet patches the URL filters, applying a saved search pushes its params
  - [ ] 5.2 `relevance` sort option; default the UI to it when `q` is present (`use-bookmark-query.ts`, `bookmark-filters.tsx`)
  - [ ] 5.3 Client-side `<mark>` highlighting helper + use in `bookmark-card.tsx`
  - [ ] 5.4 Facet panel rendering tag/category counts; clicking one calls the existing `patch`
  - [ ] 5.5 `api/searches.ts` TanStack Query hooks + Save-search button and saved-search list
  - [ ] 5.6 Verify tests and `npm run build` pass

## Notes

- No new packages: `tsvector`/`NpgsqlTsVector` ship with `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.2.
- Tasks 1–4 are backend and land in order (2 depends on 1's column; 3 shares 2's filtered query). Task 5 depends on 2 and 3 being deployed.
- The dev database now holds 434 real bookmarks, which makes ranking quality and index behaviour observable rather than theoretical.

# Spec Tasks

- [x] 1. Generated `tsvector` column + GIN index
  - [x] 1.1 `Tests/Infrastructure/SearchVectorTests.cs` (6 Testcontainers tests): populates on insert, A/B/C weights, recomputes on title change, URL words indexed individually, column is `is_generated = ALWAYS`, GIN index exists
  - [x] 1.2 `SearchVector` (`NpgsqlTsVector`) added to `Models/Bookmark.cs`; not on `BookmarkDto`
  - [x] 1.3 **Spec's API assumption was wrong**: `HasGeneratedTsVectorColumn` has a single overload `(builder, vector, config, include)` with **no weights parameter** — it emits an unweighted `to_tsvector`, and `SetWeight` is query-side only. Used `HasComputedColumnSql(..., stored: true)` with the explicit `setweight` SQL from database-schema.md instead. Guarded by `Database.IsNpgsql()` — the InMemory provider can't map `tsvector` and fails model validation otherwise
  - [x] 1.4 Migration `20260714133222_AddBookmarkSearchVector` — stored generated column + GIN index. Applied to the dev database on restart: **434/434 rows populated, 0 lost** (a stored generated column backfills itself — no data migration step). Verified on real data: `EXPLAIN` shows `Bitmap Index Scan on IX_Bookmarks_SearchVector` (index used, not a seq scan); "github" ranks title hits at 0.7599 vs url-only at 0.1216 across 11 + 36 matches, confirming the weights order results
  - [x] 1.5 72/72 backend tests pass (66 pre-existing + 6 new)

  **Deviation from spec, deliberate:** the `Url` is normalized (`regexp_replace(Url, '[^a-zA-Z0-9]+', ' ', 'g')`) before `to_tsvector`. Postgres' parser indexes a URL as host/url_path tokens (`wolverine.netlify.app`, `/guide`), so a search for `wolverine` would **not** match `https://wolverine.netlify.app` — a regression against the `ILIKE '%term%'` search being replaced. Splitting to words keeps each segment matchable. Pinned by `SearchVector_IndexesUrlWordsIndividually_NotAsHostTokens`.

- [x] 2. Ranked search in `GetBookmarksAsync`
  - [x] 2.1 `Tests/Infrastructure/SearchQueryTests.cs` (17 tests): title/description/url matching, stemming, case-insensitivity, tag-name match, title-outranks-url, relevance default vs explicit sort, websearch syntax, filter+pagination composition, empty result, 5 malformed inputs, `sortBy=relevance` with no term, tie determinism. Retired `Search_IsCaseInsensitive_ViaILike` (named for the removed mechanism); the guarantee moved to `Search_IsCaseInsensitive`
  - [x] 2.2 `ILike` block replaced with a tsvector match `UNION` a tag-name match (see below). Extension names confirmed against the package XML. Search stays Postgres-only — no InMemory fallback, since `ILIKE` was equally Postgres-only and no InMemory test exercises search
  - [x] 2.3 `relevance` added to `ApplySort` (now takes the search term); defaults to relevance when `search` is set and `sortBy` isn't; `sortBy=relevance` with no term falls back to the date default instead of failing
  - [x] 2.4 88/88 backend tests pass (72 + 17 new − 1 retired)

  **Deviation from spec — UNION instead of OR.** The spec prescribes the filter
  `b.SearchVector.Matches(tsquery) || b.BookmarkTags.Any(...)`. Implemented literally, all tests passed —
  and `EXPLAIN` on the real 434-row database showed **`Seq Scan on "Bookmarks"`**: a disjunction spanning
  two tables makes the planner abandon the GIN index and read every row, defeating the index this spec
  exists to add (and its "stays fast as the collection grows" user story). Rewritten as
  `WHERE b.Id IN (SELECT id WHERE tsvector @@ q UNION SELECT id WHERE tag ILIKE ...)`, which lets each
  branch use its own index: **plan cost 9678 → 59** on 434 rows, widening with collection size. Matching
  on ids keeps the `Include`s on the outer query (EF can't `Include` across a set operation). EF's
  generated SQL was inspected to confirm the `IN (… UNION …)` shape and that `ts_rank` ordering survives.
  ⚠️ The *exact* EF shape has not been `EXPLAIN`ed on the 434-row database (the hand-written equivalent
  has); worth confirming next time the AppHost is up.

  **Measured on real data (434 bookmarks), old ILIKE → new FTS hits:** `deployed` 0 → 9, `running` 0 → 3,
  `testing` 3 → 8, `authentication` 22 → 25. Stemming finds "Test Driven Development" for "testing" —
  impossible with substring matching.

  **Added beyond spec — tie-breaking.** Identically-shaped matches score identical ranks (real data shows 5 rows at 0.7734), and Postgres may return tied rows in any order, so paging could repeat or skip rows between requests. Relevance sort now breaks ties with `DateAdded desc, Id`, making the order total. Pinned by `Search_TiedRanks_AreOrderedDeterministically`.

  **Known, spec-sanctioned:** tag names are outside the tsvector, so a tag-only hit has rank 0 and sorts below every text hit. The spec puts trigger-maintained tag ranking out of scope.

  **Test-quality note:** three tests initially passed against the *old* `ILIKE` code for the wrong reasons — the tag test's URL contained the search token, and both ranking tests created the title-match second so `dateAdded desc` ordered them correctly by accident. Rewritten to isolate the behaviour under test before implementing.

- [x] 3. Facets in the list response
  - [x] 3.1 `Tests/Infrastructure/FacetTests.cs` (7 tests): absent when nothing narrows, tag counts span the whole match not the page, counts reflect the search, category counts (uncategorised excluded), present for a filter without a search, empty on no matches, and the old `PagedResult<BookmarkDto>` shape still deserialises
  - [x] 3.2 `BuildFacetsAsync` groups tags and categories over the filtered query **before** `Skip`/`Take`. `Include`s moved to the page query only — the count and facet aggregates project their own shapes and shouldn't pay for the joins
  - [x] 3.3 `BookmarkListResult : PagedResult<BookmarkDto>` adds nullable `Facets`. Inheritance keeps `PagedResult<T>` meaningful (it's generic and lives in `Common/`, so bookmark-specific facets don't belong on it) and the addition is purely additive — pinned by the backward-compatibility test
  - [x] 3.4 104/104 backend tests pass (97 + 7 new)

  **Facets are computed only when something narrows the set** (`HasNarrowingFilters`), matching the spec: two extra aggregate queries on every unfiltered list load would buy nothing, since there's nothing to refine.

  **EF translation note:** ordering must sit on the grouping (`OrderByDescending(g => g.Count())`), not the projection. `OrderByDescending(f => f.Count)` over the already-projected `FacetItemDto` fails to translate at runtime — it threw a 500, not a compile error.

  **Test-fixture note:** `Facets_ReflectTheSearchFilter` failed twice against correct code because the fixture leaked matches into the "excluded" bookmark — first via its **url** (url words are indexed since Task 1), then via its **tag name** (search matches tag names since Task 2). Both features working as designed; the test data had to stop containing the search token.

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

## Known issues

- ⚠️ **Flaky integration suite (~1 full-suite run in 8).** `SearchVectorTests` fails intermittently
  (once with 2 failures) with no assertion message — i.e. an exception, not a bad expectation. It never
  fails in isolation (12/12 green), only when the whole suite runs, so it is cross-class interference:
  three test classes each spin up their own PostgreSQL container concurrently via
  `IClassFixture<PostgresApiFactory>`, and `PostgresApiFactory` also sets the process-global
  `EF_DESIGN_MODE` env var. Task 2 added the third such class, which likely tipped it over.
  Suggested fix (own change, not this spec): move the Postgres classes into one xUnit collection with
  `ICollectionFixture<PostgresApiFactory>` so they share a single container and run sequentially —
  fewer containers, faster, no contention. Tests already use unique tokens, so sharing a database is safe.

## Notes

- No new packages: `tsvector`/`NpgsqlTsVector` ship with `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.2.
- Tasks 1–4 are backend and land in order (2 depends on 1's column; 3 shares 2's filtered query). Task 5 depends on 2 and 3 being deployed.
- The dev database now holds 434 real bookmarks, which makes ranking quality and index behaviour observable rather than theoretical.

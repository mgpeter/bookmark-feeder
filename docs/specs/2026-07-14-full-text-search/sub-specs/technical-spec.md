# Technical Specification

This is the technical specification for the spec detailed in @docs/specs/2026-07-14-full-text-search/spec.md

## Technical Requirements

### 1. Generated tsvector column + GIN index (BookmarkFeeder.WebApi)

- Add a generated `tsvector` search column to `Bookmark` (`Models/Bookmark.cs`), typed
  `NpgsqlTypes.NpgsqlTsVector` (excluded from `BookmarkDto`).
- Configure in `Data/BookmarkDbContext.cs`:
  `entity.HasGeneratedTsVectorColumn(b => b.SearchVector, "english", b => new { b.Title, b.Description, b.Url })`
  with weights (Title = A, Description = B, Url = C) and a GIN index
  `entity.HasIndex(b => b.SearchVector).HasMethod("GIN")`. Nullable `Description` is coalesced by
  the generated expression. **Verify at execution:** exact Npgsql 10 weighting API
  (`HasGeneratedTsVectorColumn` overload vs. explicit `setweight` expression).
- EF migration adds the stored generated column + GIN index. Existing rows populate automatically
  (generated column). Real behavior is covered by the Testcontainers suite (`PostgresApiFactory`).

### 2. Ranked search in `GetBookmarksAsync` (`Services/BookmarkService.cs`)

- Replace the current `EF.Functions.ILike` block. When `query.Search` is non-empty:
  - Build a query: `var tsquery = EF.Functions.WebSearchToTsQuery("english", term)` (websearch
    syntax supports quotes/OR/negation).
  - Filter: `b.SearchVector.Matches(tsquery) || b.BookmarkTags.Any(bt => EF.Functions.ILike(bt.Tag.Name, $"%{term}%"))`
    (tsvector match OR tag-name match).
  - Rank: order by `b.SearchVector.Rank(tsquery)` descending when the sort is relevance.
  - **Verify at execution:** exact Npgsql extension names (`Matches`, `Rank`, `WebSearchToTsQuery`).
- Sorting: add `relevance` to the `sortBy` options; when `search` is present and `sortBy` is unset,
  default to `relevance`. Otherwise the existing sort switch (`ApplySort`) still applies.
- The tag/category/read/sourceFolder/date filters and pagination are unchanged and compose with search.

### 3. Facets (`Services/BookmarkService.cs`, response shape)

- Over the same filtered `IQueryable<Bookmark>` (before Skip/Take), compute:
  - Tag facets: group by joined tag → `{ id, name, count }`.
  - Category facets: group by `CategoryId` (non-null) → `{ id, name, count }`.
- Return facets alongside the page. Add a nullable `Facets` to the bookmarks response
  (`Common/PagedResult.cs` or a dedicated `BookmarkListResult`), populated whenever a search or
  filters are active; `null`/empty otherwise. Keep it additive so existing consumers are unaffected.

### 4. Saved searches (`BookmarkFeeder.WebApi`)

- New entity `SavedSearch` (`Models/SavedSearch.cs`): `Guid Id`, `string Name` (unique-ish),
  `string Query` (the serialized query string / filter params, e.g. `q=...&tags=...`),
  `DateTime DateCreated`. Configured in `BookmarkDbContext`; migration adds the `SavedSearches` table.
- `ISavedSearchService`/`SavedSearchService` (consuming `IDbContextFactory`) + `SavedSearchEndpoints`
  under `/api/searches` (`MapGroup("/searches")` in `Program.cs`, inside the `/api` group so it gets
  the API-key filter + `reads`/`writes` rate limits like the others).
- Single-tenant (no user scoping).

### 5. Frontend (BookmarkFeeder.Web)

- Add a `relevance` option to the sort control; default the UI to relevance when a search term is
  present (`features/bookmarks/use-bookmark-query.ts`, `bookmark-filters.tsx`).
- Highlight the search term(s) in each card's title/description client-side (wrap matches in
  `<mark>`) - a small helper + use in `bookmark-card.tsx`. No server highlight field needed.
- Facet panel: render tag/category counts from the response; clicking a facet adds it to the
  filters (reuses the existing URL-param `patch`).
- Saved searches: `api/searches.ts` TanStack Query hooks (`useSavedSearches`, create, delete); a
  small UI (e.g. a "Save search" button capturing the current query string + a dropdown/list to
  apply a saved one by pushing its params to the URL).

## External Dependencies

None. `tsvector`/`NpgsqlTsVector` and the FTS query functions are provided by the already-referenced
`Npgsql.EntityFrameworkCore.PostgreSQL` (10.0.2); no new packages.

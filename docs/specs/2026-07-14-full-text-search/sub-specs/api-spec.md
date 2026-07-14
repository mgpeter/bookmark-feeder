# API Specification

This is the API specification for the spec detailed in @docs/specs/2026-07-14-full-text-search/spec.md

## Modified: GET /api/bookmarks

**Purpose:** Same list endpoint, now full-text-search-backed and facet-returning.

**Parameters (changed/added):**
- `search` — now interpreted as a full-text query (websearch syntax: quotes, `OR`, `-term`);
  matches title/description/url (ranked) or tag names.
- `sortBy` — adds `relevance` (in addition to `dateAdded`, `dateModified`, `title`, `url`).
  When `search` is present and `sortBy` is omitted, results default to `relevance`.
- All existing filters (`tags`, `categories`, `sourceFolder`, `isRead`, `dateFrom`, `dateTo`),
  `page`, `pageSize`, `sortOrder` are unchanged and compose with `search`.

**Response (added `facets`):**
```json
{
  "data": [ /* BookmarkDto[] (unchanged) */ ],
  "pagination": { "page": 1, "pageSize": 20, "totalItems": 42, "totalPages": 3 },
  "facets": {
    "tags": [ { "id": "…", "name": "dotnet", "count": 12 } ],
    "categories": [ { "id": "…", "name": "Technology", "count": 8 } ]
  }
}
```
`facets` is present when a search/filter is active (may be `null` or empty otherwise). `BookmarkDto`
itself is unchanged — term highlighting is applied client-side.

**Errors:** unchanged (401 without key; 429 when rate-limited).

## New: Saved searches — /api/searches

All under the `/api` group (require `X-API-Key`; rate-limited: reads/writes).

### GET /api/searches
**Purpose:** List saved searches.
**Response:** `SavedSearchDto[]` — `{ id, name, query, dateCreated }`.

### POST /api/searches
**Purpose:** Save the current query + filters.
**Parameters (body):** `{ name: string, query: string }` (`query` = the serialized param string,
e.g. `q=graphql&tags=dotnet&sortBy=relevance`).
**Response:** `201 Created` with the `SavedSearchDto`.
**Errors:** `400` on validation failure (empty name/query).

### DELETE /api/searches/{id}
**Purpose:** Remove a saved search.
**Response:** `204 No Content`; `404` if not found.

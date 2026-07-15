# API Specification

This is the API specification for the spec detailed in @docs/specs/2026-07-14-mark-all-as-read/spec.md

## New: POST /api/bookmarks/mark-read

**Purpose:** Set the read state of every bookmark matching a filter set, in one statement - the
bulk counterpart to `PATCH /api/bookmarks/{id}/read`.

**Parameters (query string):** the same `BookmarkQuery` the list endpoint binds, with identical
semantics - `search`, `tags`, `categories`, `sourceFolder`, `isRead`, `dateFrom`, `dateTo`. The
client sends the same filter string it used for the `GET`, so the affected set is exactly the set
shown.

`page`, `pageSize`, `sortBy` and `sortOrder` are accepted (they're part of `BookmarkQuery`) but
**ignored** - the action spans every match across all pages by design.

With no filter parameters, every non-deleted bookmark matches. This is allowed; the UI's
confirmation dialog is what guards it.

**Parameters (body):** the existing `MarkReadRequest`.

```json
{ "isRead": true }
```

The target state lives in the body because `isRead` in the query string is already the read-state
*filter*. The two are different things: `?isRead=false` with body `{ "isRead": true }` means "mark
the currently-unread ones as read".

**Response:** `200 OK`

```json
{ "updated": 137 }
```

`updated` counts rows whose state actually changed. Bookmarks already in the target state are
skipped, so this can be lower than the matching total (`pagination.totalItems` from the `GET`) -
the dialog reports the set, this reports the change.

**Errors:**
- `401 Unauthorized` - missing/invalid `X-API-Key` (inherited from the `/api` group).
- `429 Too Many Requests` - the `writes` rate-limit policy.
- `400 Bad Request` - malformed body.

**Example**

```http
POST /api/bookmarks/mark-read?q=graphql&tags=dotnet&isRead=false
X-API-Key: …
Content-Type: application/json

{ "isRead": true }
```

Marks every unread bookmark matching "graphql" tagged `dotnet` as read, across all pages.

## Unchanged

`GET /api/bookmarks`, `PATCH /api/bookmarks/{id}/read` and the rest of the bookmark endpoints are
unaffected. The filter-composition extraction behind `GET /api/bookmarks` is internal refactoring
with no contract change.

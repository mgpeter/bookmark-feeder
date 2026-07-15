# API Specification

This is the API specification for the spec detailed in @docs/specs/2026-07-14-import-export/spec.md

## New: POST /api/bookmarks/import

**Purpose:** Import bookmarks from an uploaded export file.
**Request:** `multipart/form-data` with a `file` part (the export) and an optional `format` field
(`auto` default; or `netscape` | `pocket` | `instapaper` | `json` | `csv`).
**Behavior:** Auto-detects the format, parses entries (folder path → `sourceFolder`, source tags →
tags), and inserts through the batch path with `skipDuplicates = true`.
**Response:** `200 OK` with the batch summary:
```json
{
  "created": [ { "id": "…", "url": "…", "status": "created" } ],
  "skipped": [ { "url": "…", "status": "duplicate" } ],
  "errors":  [ { "url": "…", "message": "…" } ],
  "summary": { "total": 120, "created": 98, "skipped": 20, "errors": 2 }
}
```
**Errors:** `400` if the file is missing/unparseable/unsupported; `401` without the key; `429` when
rate-limited (`writes`/`sync` policy).

## New: GET /api/bookmarks/export

**Purpose:** Download the collection (optionally filtered) in an open format.
**Parameters (query):**
- `format` - `json` (default) | `html` | `csv`.
- Any existing `BookmarkQuery` filter params (`search`, `tags`, `categories`, `sourceFolder`,
  `isRead`, `dateFrom`, `dateTo`, `sortBy`, `sortOrder`) to export a subset; omitted = whole collection.
**Response:** `200 OK` file download with `Content-Disposition: attachment; filename="bookmarks-<date>.<ext>"`:
- `json` → `application/json` (array of full bookmark objects)
- `html` → `text/html` (Netscape bookmark file; `sourceFolder` → folders; tags via `TAGS` attribute)
- `csv`  → `text/csv` (`url,title,description,tags,category,sourceFolder,isRead,dateAdded`)
**Errors:** `400` on an unknown `format`; `401` without the key; `429` when rate-limited (`reads`).

## Modified: POST /api/bookmarks/batch

**Change (additive):** `BatchBookmarkItem` gains an optional `tags: string[]` (per-item), resolved
and attached (resolve-or-create by name) alongside the request-level `defaultTags`. Existing callers
(the browser extension) are unaffected when the field is omitted.

# Technical Specification

This is the technical specification for the spec detailed in @docs/specs/2026-07-14-import-export/spec.md

## Technical Requirements

### 1. Reuse the batch path with per-item tags (BookmarkFeeder.WebApi)

- Extend `BatchBookmarkItem` (`Dtos/BookmarkDtos.cs`) with an optional `string[]? Tags` (per-item).
- Update `BookmarkService.CreateBatchAsync` (`Services/BookmarkService.cs`) to resolve-and-attach
  each item's own tags (reusing `ResolveTagsAsync`) in addition to `DefaultTags`. This is additive
  and backward-compatible (existing callers/extension unaffected), and lets import carry per-item
  tags through the existing URL de-duplication (incl. soft-deleted rows).

### 2. Import service (`IImportService` / `ImportService`)

- `POST /api/bookmarks/import` accepts a multipart file upload (`IFormFile`) + optional `format`
  hint. **Auto-detect** the format by sniffing content:
  - **Netscape bookmarks HTML** (`<!DOCTYPE NETSCAPE-Bookmark-file-1>` / nested `DL>DT>A`, `H3`
    folders) - browsers, and Instapaper's HTML export.
  - **Pocket HTML** (`ul>li>a` with a `tags` attribute) - and Pocket CSV.
  - **JSON** (our export shape) and generic **CSV** (`url,title,tags,…`).
- Parse HTML with **AngleSharp**; parse CSV with **CsvHelper**; JSON with `System.Text.Json`.
- Map each entry → `BatchBookmarkItem`: `Url`, `Title`, `DateAdded` (epoch when present),
  `SourceFolder` = the ancestor `H3` folder path joined by `/` (Netscape), `Tags` = source tags
  (Pocket/Instapaper). Skip entries without a valid absolute URL.
- Insert via `CreateBatchAsync(new BatchCreateRequest(items, SkipDuplicates: true))`; return its
  `BatchResultDto` summary (created / skipped / errors / totals). Enforce a sane request size limit.

### 3. Export service (`IExportService` / `ExportService`)

- `GET /api/bookmarks/export?format=json|html|csv` plus the existing `BookmarkQuery` filter params
  (so a filtered/searched view can be exported; default = whole collection). Loads bookmarks with
  tags + category via the existing query path (no pagination).
- Formats:
  - **json** - an array of full bookmark objects (url, title, description, tags, category,
    sourceFolder, isRead, dateAdded, dateModified). `Content-Type: application/json`.
  - **html** - Netscape bookmark file (grouped by `sourceFolder` as `H3` folders; tags emitted as
    the `TAGS` attribute on `<A>` for round-trip). `text/html`.
  - **csv** - `url,title,description,tags,category,sourceFolder,isRead,dateAdded` via CsvHelper.
    `text/csv`.
- Return as a download (`Content-Disposition: attachment; filename="bookmarks-<date>.<ext>"`).

### 4. Endpoints & wiring

- Add `ImportExportEndpoints` (or extend `BookmarkEndpoints`) under the `/api/bookmarks` group so
  both routes get the API-key filter. Rate limits: import → `writes` (or `sync`), export → `reads`.
  Register `IImportService`/`IExportService` (scoped, `IDbContextFactory`).

### 5. Frontend (BookmarkFeeder.Web)

- `api/import-export.ts` TanStack Query hooks: `useImportBookmarks` (POST multipart `FormData`) and
  an `exportBookmarks(format, query)` helper that triggers a file download (fetch → blob → anchor,
  attaching `X-API-Key` via the existing client).
- UI: an **Import** action (file input → upload → toast the summary + invalidate the bookmarks
  query) and an **Export** action (format menu → download), in the bookmarks page header.

### 6. Testing

- Parser unit tests: sample Netscape HTML (nested folders → sourceFolder), Pocket HTML/CSV (tags),
  and our JSON/CSV → correct `BatchBookmarkItem`s; malformed input handled gracefully.
- Integration tests: `POST /import` of a small fixture returns the summary and creates bookmarks
  (dedup skips repeats); `GET /export?format=…` returns each content type with the expected shape.

## External Dependencies

- **AngleSharp** - Robust HTML parsing for Netscape/Pocket/Instapaper imports. (May already be added
  by the favicon-enrichment spec; share it.)
- **CsvHelper** - Correct CSV reading/writing (quoting/escaping) for import and export.
  - **Justification:** hand-rolled CSV is error-prone with quoted fields/embedded commas;
    both are widely-used, well-maintained libraries. Pin latest stable at implementation.

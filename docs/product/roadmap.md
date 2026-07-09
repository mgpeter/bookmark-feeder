# Product Roadmap

## Phase 0: Already Completed

Foundation, data layer, API, and browser-extension sync are implemented and
tested (56 backend tests passing).

- [x] .NET 10 + Aspire 13 upgrade across all projects
- [x] Bookmark / Tag / Category data model on EF Core 10 + PostgreSQL (soft delete, unique URL, single join table)
- [x] Full REST API — Bookmarks/Tags/Categories CRUD, batch sync, pagination + filtering, duplicate detection `L`
- [x] X-API-Key authentication (endpoint filter) + OpenAPI/Scalar with pre-filled dev key
- [x] Browser extension end-to-end sync (recursive folder traversal, batch upload with API key) `M`
- [x] Database migration, dev seed data, and integration tests `M`

## Phase 1: Web Frontend (Current)

**Goal:** A usable React web app to browse, search, edit, and organize the collection.
**Success Criteria:** From a browser, connect via API key, view seeded bookmarks, filter/search, edit a bookmark, and manage tags/categories.

### Features

- [x] Scaffold (React + Vite + Tailwind v4 + shadcn/ui) + Aspire `AddViteApp` wiring `M`
- [x] API connection: Settings screen (URL + X-API-Key), fetch client, 401→Settings, TanStack Query layer `S`
- [x] App shell, sidebar navigation, dashboard with live counts `S`
- [x] Bookmark list: search, tag/category/read filters, sort, grid/list, pagination, quick actions `L`
- [ ] Bookmark edit dialog (retag, recategorize, mark read) `M`
- [ ] Tag management (create/rename/recolor/delete) `S`
- [ ] Category management (tree, reparent, delete-with-reassign) `M`
- [ ] Dashboard polish, empty/error states, source-folder + date-range filters, Vitest tests `M`

### Dependencies

- Phase 0 REST API (done)

## Phase 2: Production & Deployment

**Goal:** Run BookmarkFeeder reliably on a self-hosted Docker/NAS environment.
**Success Criteria:** `docker compose up` brings up Postgres + API (serving the built web app) behind a reverse proxy; health probes green; rate limits enforced.

### Features

- [ ] Docker Compose (Postgres + API serving the static SPA from wwwroot) `L`
- [ ] Production build pipeline (Vite build → wwwroot, `MapFallbackToFile`) `M`
- [ ] Reverse proxy + SSL/TLS guidance (Nginx/Traefik) `M`
- [ ] Production health/readiness endpoints + structured logging `S`
- [ ] Per-endpoint rate limiting (bulk/sync, CRUD, search) `M`
- [ ] Integration tests against real PostgreSQL (Testcontainers) `M`

### Dependencies

- Phase 1 web frontend

## Phase 3: Search & Discovery

**Goal:** Fast, ranked full-text search across the collection.
**Success Criteria:** Typical queries return ranked, highlighted results in under 1s.

### Features

- [ ] PostgreSQL full-text search (tsvector + GIN index) replacing the ILIKE scan `L`
- [ ] Relevance ranking, result highlighting, and facets (tags/categories) `M`
- [ ] Search UI: query bar, highlights, facet filters `M`
- [ ] Saved searches + search history `M`

### Dependencies

- Phase 1 web frontend (search UI hooks already present)

## Phase 4: AI Categorization

**Goal:** Auto-suggest tags and categories with a review workflow.
**Success Criteria:** New bookmarks receive high-confidence suggestions; user can approve/auto-apply in bulk.

### Features

- [ ] LLM client + prompt engineering (provider decision) `M`
- [ ] Background job queue for batch categorization (202 + jobId, polled status) `L`
- [ ] Confidence scoring, cost controls, graceful degradation when AI is unavailable `M`
- [ ] Approval workflow UI (review, bulk approve/reject, manual override) `M`

### Dependencies

- Phase 1 web frontend (approval UI)

## Phase 5: Advanced (Future)

**Goal:** Round out import/export, richer metadata, and optional multi-user.

### Features

- [ ] Import from browser HTML / Pocket / Instapaper; export JSON/HTML/CSV `L`
- [ ] Favicon enrichment service + content extraction `M`
- [ ] Scheduled/background extension sync `M`
- [ ] Optional multi-user (User entity + JWT, per-user data) `XL`

### Dependencies

- Phases 2-4

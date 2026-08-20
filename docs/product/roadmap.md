# Product Roadmap

## Phase 0: Already Completed

Foundation, data layer, API, and browser-extension sync are implemented and
tested (56 backend tests passing).

- [x] .NET 10 + Aspire 13 upgrade across all projects
- [x] Bookmark / Tag / Category data model on EF Core 10 + PostgreSQL (soft delete, unique URL, single join table)
- [x] Full REST API - Bookmarks/Tags/Categories CRUD, batch sync, pagination + filtering, duplicate detection `L`
- [x] X-API-Key authentication (endpoint filter) + OpenAPI/Scalar with pre-filled dev key *(superseded by DEC-008)*
- [x] Browser extension end-to-end sync (recursive folder traversal, batch upload with API key) `M` *(the key is now per-user, DEC-008)*
- [x] Database migration, dev seed data, and integration tests `M`

## Phase 1: Web Frontend

**Goal:** A usable React web app to browse, search, edit, and organize the collection.
**Success Criteria:** From a browser, connect via API key, view seeded bookmarks, filter/search, edit a bookmark, and manage tags/categories.

### Features

- [x] Scaffold (React + Vite + Tailwind v4 + shadcn/ui) + Aspire `AddViteApp` wiring `M`
- [x] API connection: Settings screen (URL + X-API-Key), fetch client, 401→Settings, TanStack Query layer `S` *(superseded by DEC-008)*
- [x] App shell, sidebar navigation, dashboard with live counts `S`
- [x] Bookmark list: search, tag/category/read filters, sort, grid/list, pagination, quick actions `L`
- [x] Bookmark edit dialog (retag, recategorize, mark read) `M`
- [x] Tag management (create/rename/recolor/delete) `S`
- [x] Category management (tree, reparent, delete-with-reassign) `M`
- [x] Dashboard polish, empty/error states, source-folder + date-range filters, Vitest tests `M`

### Dependencies

- Phase 0 REST API (done)

## Phase 2: Production & Deployment

**Goal:** Run BookmarkFeeder reliably on a self-hosted Docker/NAS environment.
**Success Criteria:** `docker compose up` brings up Postgres + api + web behind a YARP gateway on a single exposed port; health probes green; rate limits enforced.

### Features

- [x] YARP reverse-proxy gateway - single external origin (`/api`→api, `/`→web), dev + prod `M`
- [x] Web served as its own container (Vite dev / static nginx); relative `/api`, no CORS `M`
- [x] API production hardening (forwarded headers, opt-in HTTPS redirect, prod health endpoints) `S`
- [x] Per-endpoint rate limiting (bulk/sync, CRUD, search) `M`
- [x] Integration tests against real PostgreSQL (Testcontainers) `M`
- [x] Aspire publish → Docker Compose (gateway + api + web + postgres) `L`

### Dependencies

- Phase 1 web frontend

## Phase 3: Search & Discovery

**Goal:** Fast, ranked full-text search across the collection.
**Success Criteria:** Typical queries return ranked, highlighted results in under 1s.

### Features

- [x] PostgreSQL full-text search (tsvector + GIN index) replacing the ILIKE scan `L`
- [x] Relevance ranking, result highlighting, and facets (tags/categories) `M`
- [x] Search UI: query bar, highlights, facet filters `M`
- [x] Saved searches `M` (search *history* not built - out of scope in the spec)

### Dependencies

- Phase 1 web frontend (search UI hooks already present)

## Phase 4: AI Categorization (Current)

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

**Goal:** Round out import/export and richer metadata. (Multi-user shipped early - see Phase 6.)

### Features

- [ ] Import from browser HTML / Pocket / Instapaper; export JSON/HTML/CSV `L`
- [x] Favicon enrichment service `M` (content extraction not built)
- [ ] Scheduled/background extension sync `M`
- [x] Multi-user - moved to Phase 6 and delivered ahead of schedule `XL`

### Dependencies

- Phases 2-4

## Phase 6: Multi-User (Delivered)

**Goal:** Real accounts and per-user data isolation, so the deployment can be shared without
sharing the collection.
**Success Criteria:** Two accounts see two independent collections; neither can read, modify or
even detect the other's, proven by integration tests. The browser extension authenticates with a
key that cannot delete anything.

### Features

- [x] Auth0 sign-in for the web app (OIDC, Authorization Code + PKCE, RS256 bearer) `L`
- [x] User entity + just-in-time provisioning; the first login claims the pre-migration collection `M`
- [x] OwnerId on Bookmark/Tag/Category/SavedSearch, enforced by EF Core 10 named query filters `L`
- [x] Per-user, sync-scoped API keys for the extension: hashed, shown once, individually revocable `M`
- [x] Per-credential rate limiting; per-owner unique indexes; runtime Auth0 config for the prebuilt web image `M`

### Dependencies

- An Auth0 tenant (free tier)
- TLS in front of the gateway - see DEC-008

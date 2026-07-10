# API Specification

This is the API specification for the spec detailed in @docs/specs/2026-07-09-production-deployment/spec.md

This spec adds no new business endpoints. The externally visible HTTP surface is shaped
by the **gateway** routing plus two API changes (production health probes and rate-limit
responses). The API itself no longer serves any static/SPA content.

## Gateway routing (BookmarkFeeder.Gateway)

The gateway is the only external origin. Two ordered routes:

### `/api/{**catch}` → api

**Purpose:** Forward all API traffic to the WebApi service (preserving method, body, and
the `X-API-Key` header).
**Response:** whatever the API returns (JSON). An unknown `/api/*` route returns the API's
`404` (not HTML), because it is handled by the API, not the web fallback.

### `/{**catch}` → web

**Purpose:** Serve the SPA (Vite dev server in dev, static nginx in prod). Client routes
like `/bookmarks` resolve to `index.html` via the web container's own SPA fallback
(`try_files … /index.html`). WebSocket upgrades pass through (Vite HMR in dev).

## Health & readiness (API)

### GET /health

**Purpose:** Readiness — is the app able to serve (including the DB check)?
**Response:** `200 Healthy` when checks pass; `503 Unhealthy` when a check fails.
**Change:** now mapped in Production (previously Development-only).

### GET /alive

**Purpose:** Liveness — is the process up?
**Response:** `200 Healthy`.
**Change:** now mapped in Production. The gateway also exposes its own `/alive`.

## Rate limiting (API)

**Applies to:** all `/api/*` routes, with a stricter policy on `POST /api/bookmarks/batch`
and mutating routes.

**Behavior on limit exceeded:**
- **Response:** `429 Too Many Requests`
- **Headers:** `Retry-After: <seconds>`.
- **Partition:** the `X-API-Key` header (fallback to forwarded client IP), so one client's
  traffic doesn't throttle another. Successful responses below the limit are unchanged.

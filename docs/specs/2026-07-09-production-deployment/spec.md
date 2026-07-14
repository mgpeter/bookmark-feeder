# Spec Requirements Document

> Spec: Production Deployment
> Created: 2026-07-09
> Status: Completed
> Completed: 2026-07-14

## Overview

Make BookmarkFeeder self-hostable on a NAS via Docker Compose using a clean
multi-container topology: a **YARP reverse-proxy gateway** is the single external
entry point that routes `/api` to the API and everything else to a static web
container, with PostgreSQL behind them. The same gateway runs in Aspire for dev, so
there is one routing model across environments — no CORS for the web app and no
frontend path rewrites. Compose artifacts are produced by Aspire's publish pipeline,
and the data layer is validated against a real PostgreSQL container.

## User Stories

### Self-host on a NAS

As a self-hosting enthusiast, I want to deploy BookmarkFeeder with `docker compose up`
and expose a single port, so that I can run it on my Synology NAS behind one URL.

The operator brings up four services (gateway, api, web, postgres), sets a few
environment variables (API key, DB password), and reaches everything through the
gateway's port. The API applies EF migrations on startup.

### One URL, no CORS

As a user, I want the whole app behind one origin, so that there is no CORS setup and
the browser only talks to one host. The gateway serves the web app at `/` and forwards
`/api/*` to the API; the frontend just calls a relative `/api`.

### Same model in dev and prod

As the developer, I want dev to route exactly like prod, so that I don't debug
environment-specific wiring. The YARP gateway is an Aspire resource in dev and a
published container in prod; the frontend code is identical in both.

### Protected from overload

As an operator, I want per-endpoint rate limits, so that the batch/sync and CRUD
endpoints can't be hammered into resource exhaustion.

## Spec Scope

1. **YARP gateway (single entry, dev + prod)** - A .NET YARP reverse-proxy that routes
   `/api/{**}` → the API and `/{**}` → the web app, using Aspire service discovery for
   destinations; it is the only externally exposed endpoint.
2. **Web app as its own container** - The React app is served by the Vite dev server in
   dev and a static nginx container in prod; it calls a relative `/api` (no absolute base
   URL), and its Settings screen needs only the API key.
3. **API production hardening** - Add forwarded-headers support, make HTTPS redirect
   opt-in (off behind the gateway), and expose `/health` + `/alive` in production.
4. **Per-endpoint rate limiting** - ASP.NET Core rate limiter with a strict limit on
   batch/sync and sensible CRUD/search limits, returning `429` with `Retry-After`,
   partitioned by API key.
5. **Aspire publish → Docker Compose** - Emit compose + images for gateway, api, web,
   and postgres; only the gateway exposes a host port; the Postgres volume persists data.
6. **Real-PostgreSQL integration tests** - Testcontainers-backed xUnit tests covering the
   unique-URL index, `ILIKE` search, and soft-delete behaviors EF InMemory can't validate.

## Out of Scope

- HTTPS/TLS termination at the gateway (Let's Encrypt, certs) — deferred; the gateway
  serves plain HTTP for a LAN or an outer proxy to terminate TLS.
- Multi-user auth, secret-manager integration, and CI/CD pipelines.
- Backups and wiring OpenTelemetry to a monitoring backend.
- Search and AI features (later roadmap phases).

## Expected Deliverable

1. Aspire publish produces compose + images; `docker compose up` brings up gateway, api,
   web, and postgres; migrations apply; the app loads through the **gateway's single
   port**, and only that port is exposed.
2. Visiting `/` serves the SPA and `/bookmarks` deep-links work; `/api/*` returns JSON
   through the gateway; a request without the API key still returns `401`; no CORS is
   involved for the web app.
3. Exceeding a route's rate limit returns `429` with `Retry-After`; normal usage is
   unaffected.
4. `dotnet test` runs the Testcontainers integration tests green against real PostgreSQL.

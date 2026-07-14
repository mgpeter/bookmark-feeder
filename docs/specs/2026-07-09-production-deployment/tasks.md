# Spec Tasks

- [x] 1. Relative `/api` web app (drop absolute base URL)
  - [x] 1.1 Update the `api-client` Vitest tests to expect relative, same-origin `/api` URLs (+ `X-API-Key`)
  - [x] 1.2 Refactor `api-client` to build `new URL('/api' + path, window.location.origin)`
  - [x] 1.3 Simplify the web Settings screen to an API-key-only form; leave the browser extension unchanged
  - [x] 1.4 Verify frontend build + Vitest pass

- [x] 2. YARP gateway project + Aspire wiring
  - [x] 2.1 Verify the YARP + Aspire service-discovery destination syntax for 13.4.6 (Yarp.ReverseProxy 2.3.0, ServiceDiscovery.Yarp 10.7.0; `AddServiceDiscoveryDestinationResolver`)
  - [x] 2.2 Create `BookmarkFeeder.Gateway` (YARP): `AddServiceDefaults`, `AddReverseProxy().LoadFromConfig`, routes `/api/{**}`→webapi and `/{**}`→web, `X-API-Key` forwarded (default)
  - [x] 2.3 AppHost: `web` internal; `gateway` references `webapi` + `web` and is the only `.WithExternalHttpEndpoints()`
  - [x] 2.4 Vite `allowedHosts` + `/api` dev proxy for the direct-Vite/HMR fallback (documented in the technical spec)
  - [x] 2.5 Live-verified in Aspire dev — browser hits only the gateway; SPA loads, `/api` routes through it, HMR works

- [x] 3. API production hardening
  - [x] 3.1 WebApplicationFactory tests: forwarded-headers options configured, HTTPS redirect off by default, `/health` + `/alive` reachable without a key, `/api` still 401
  - [x] 3.2 Added `UseForwardedHeaders` (X-Forwarded-For/Proto, cleared known proxies); made `UseHttpsRedirection` opt-in via `Https:Redirect` (default off)
  - [x] 3.3 `/health` + `/alive` mapped in all environments; CORS commented as extension-only
  - [x] 3.4 Verify all tests pass (60/60)

- [x] 4. Per-endpoint rate limiting
  - [x] 4.1 Integration tests: exceeding the write limit → `429` + `Retry-After`; reads stay 200 when writes are capped
  - [x] 4.2 `AddRateLimiter` with config-driven fixed-window policies (`sync` 5, `writes` 100, `reads` 200) partitioned by `X-API-Key` (fallback to forwarded IP)
  - [x] 4.3 Group-default `reads` + per-endpoint `writes`/`sync` overrides; `UseRateLimiter`; `Retry-After` in `OnRejected`
  - [x] 4.4 Verify all tests pass (62/62)

- [ ] 5. Testcontainers integration tests (real PostgreSQL)
  - [ ] 5.1 Add `Testcontainers.PostgreSql` + a Postgres-backed fixture (start container, `MigrateAsync`, WebApplicationFactory pointing at it)
  - [ ] 5.2 Write tests for the InMemory-blind behaviors: unique-URL index / soft-deleted de-dup, `ILIKE` search, soft-delete×unique, cascade/`SetNull`
  - [ ] 5.3 Keep the fast InMemory suite; categorize the Postgres suite so it can be filtered out without Docker
  - [ ] 5.4 Verify the Testcontainers suite passes against real PostgreSQL

- [ ] 6. Aspire publish → Docker Compose (gateway + api + web + postgres)
  - [ ] 6.1 Spike: confirm the Aspire 13.4.6 compose-publish package/API, custom-Dockerfile hookup, and single-exposed-port (gateway) behavior
  - [ ] 6.2 Add a multi-stage Dockerfile for `web` (Node build → nginx static with SPA `try_files` fallback)
  - [ ] 6.3 Add `AddDockerComposeEnvironment` to the AppHost; map env (API key, DB password, connection string); persist the Postgres volume; expose only the gateway
  - [ ] 6.4 Run publish → `docker compose up`; verify one exposed port, SPA loads via gateway, `/api` works, `401` without key, `/health` green, migrations applied
  - [ ] 6.5 Add a short "Self-hosting with Docker Compose" section to the docs

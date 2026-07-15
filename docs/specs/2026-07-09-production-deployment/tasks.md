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
  - [x] 2.5 Live-verified in Aspire dev - browser hits only the gateway; SPA loads, `/api` routes through it, HMR works

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

- [x] 5. Testcontainers integration tests (real PostgreSQL)
  - [x] 5.1 Added `Testcontainers.PostgreSql` 4.13.0 + `PostgresApiFactory` (starts postgres:17-alpine, applies real migrations, WebApplicationFactory points at it)
  - [x] 5.2 Tests: ILIKE case-insensitive search, batch de-dup vs a soft-deleted row (unique index), tag-delete join cascade, category-delete SetNull
  - [x] 5.3 Fast InMemory suite kept; Postgres suite tagged `[Trait("Category","Integration")]` - filter with `--filter Category!=Integration`
  - [x] 5.4 Verify the Testcontainers suite passes against real PostgreSQL (66/66 full; 62 without Docker)

- [x] 6. Aspire publish → Docker Compose (gateway + api + web + postgres)
  - [x] 6.1 Spike: `Aspire.Hosting.Docker` 13.4.6 `AddDockerComposeEnvironment`; Vite app `PublishAsDockerFile()`; `aspire.cli` 13.4.6 for publish
  - [x] 6.2 Multi-stage `web` Dockerfile (Node build → nginx static with SPA `try_files` fallback) + `.dockerignore`
  - [x] 6.3 `AddDockerComposeEnvironment("compose")` + `web.PublishAsDockerFile()`; only the gateway is exposed
  - [x] 6.4 `aspire publish` generates a valid compose - verified by inspection (gateway = only host port; service-discovery routing env; `Authentication__ApiKey`/connection-string/forwarded-headers wired; Postgres named volume). Full image-build + `docker compose up` is the documented NAS deploy step
  - [x] 6.5 Added `docs/deployment.md` (Self-hosting with Docker Compose); gitignored the generated `publish/`

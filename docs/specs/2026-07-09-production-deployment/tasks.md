# Spec Tasks

- [x] 1. Relative `/api` web app (drop absolute base URL)
  - [x] 1.1 Update the `api-client` Vitest tests to expect relative, same-origin `/api` URLs (+ `X-API-Key`)
  - [x] 1.2 Refactor `api-client` to build `new URL('/api' + path, window.location.origin)`
  - [x] 1.3 Simplify the web Settings screen to an API-key-only form; leave the browser extension unchanged
  - [x] 1.4 Verify frontend build + Vitest pass

- [ ] 2. YARP gateway project + Aspire wiring
  - [x] 2.1 Verify the YARP + Aspire service-discovery destination syntax for 13.4.6 (Yarp.ReverseProxy 2.3.0, ServiceDiscovery.Yarp 10.7.0; `AddServiceDiscoveryDestinationResolver`)
  - [x] 2.2 Create `BookmarkFeeder.Gateway` (YARP): `AddServiceDefaults`, `AddReverseProxy().LoadFromConfig`, routes `/api/{**}`→webapi and `/{**}`→web, `X-API-Key` forwarded (default)
  - [x] 2.3 AppHost: `web` internal; `gateway` references `webapi` + `web` and is the only `.WithExternalHttpEndpoints()`
  - [x] 2.4 Vite `allowedHosts` + `/api` dev proxy for the direct-Vite/HMR fallback (documented in the technical spec)
  - [ ] 2.5 Live-verify in Aspire dev (browser → gateway only; SPA + `/api` + HMR) — code compiles; pending a live `dotnet run` of the AppHost

- [ ] 3. API production hardening
  - [ ] 3.1 Write WebApplicationFactory tests: forwarded headers respected; HTTPS redirect off by default; `/health` + `/alive` reachable in Production
  - [ ] 3.2 Add `UseForwardedHeaders`; make `UseHttpsRedirection` opt-in via config (default off)
  - [ ] 3.3 Expose `/health` + `/alive` in all environments (ServiceDefaults `MapDefaultEndpoints`); keep CORS only for the extension
  - [ ] 3.4 Verify all tests pass

- [ ] 4. Per-endpoint rate limiting
  - [ ] 4.1 Write integration tests: exceeding batch/write limits → `429` + `Retry-After`; reads under the limit succeed
  - [ ] 4.2 `AddRateLimiter` with named policies (`sync`, `writes`, `reads`) partitioned by `X-API-Key` (fallback to forwarded IP)
  - [ ] 4.3 Apply `RequireRateLimiting` to the endpoint groups; `UseRateLimiter`; set `Retry-After` in `OnRejected`
  - [ ] 4.4 Verify all tests pass

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

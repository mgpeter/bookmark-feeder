# Technical Specification

This is the technical specification for the spec detailed in @docs/specs/2026-07-09-production-deployment/spec.md

## Topology

Four services; only the **gateway** is exposed externally:

```
            ┌───────────────── gateway (YARP) ──────────────────┐  ← only exposed port
browser ──▶ │  /api/{**}  → api        /{**} → web              │
            └───────┬───────────────────────┬───────────────────┘
                    ▼                        ▼
                  api (.NET)              web (SPA)            postgres
                    └──────────────────────────────────────────▲
                       api → postgres (internal)
```

- **Dev (Aspire):** `web` = Vite dev server, `api` = the WebApi project, `gateway` = the
  YARP project, `postgres` = Aspire Postgres. The browser hits the gateway only.
- **Prod (compose):** `web` = static nginx image (the Vite build), everything else the
  same shape. Identical routing model.

## Technical Requirements

### 1. YARP gateway (new project `BookmarkFeeder.Gateway`)

- New ASP.NET Core project referencing `Yarp.ReverseProxy`; calls `AddServiceDefaults()`
  (for service discovery + health) and `AddReverseProxy().LoadFromConfig(...)`.
- Two routes (order matters - `/api` first):
  - `api`: match `/api/{**catch}` → cluster `api`.
  - `web`: match `/{**catch}` → cluster `web`.
- Cluster destinations use **Aspire service discovery** (e.g. address `http://api` and
  `http://web`); ServiceDefaults + `.WithReference(...)` inject the resolved URLs. **Verify
  at execution:** the exact YARP + Aspire service-discovery destination syntax for 13.4.6.
- WebSocket passthrough enabled so the Vite HMR socket works through the gateway in dev.
- The gateway forwards the `X-API-Key` header untouched (default YARP behavior).

### 2. AppHost wiring (`BookmarkFeeder.AppHost/Program.cs`)

- `api` and `web` become **internal** (no external endpoint). `gateway` is the only
  resource with `.WithExternalHttpEndpoints()`.
- `builder.AddProject<Projects.BookmarkFeeder_Gateway>("gateway").WithReference(api).WithReference(web).WaitFor(api)`.
- Keep `api` referencing postgres + the `Authentication__ApiKey` param; keep `web` as
  `AddViteApp("web", ...)`.

### 3. Web app: relative `/api` + Vite HMR behind the gateway

- **Frontend `api-client`**: build request URLs relative to the current origin
  (`new URL('/api' + path, window.location.origin)`) - drop the absolute base URL. Attach
  `X-API-Key` from localStorage as today.
- **Settings**: the web app's Settings screen keeps only the **API key** field (origin is
  always the gateway). The **browser extension is unchanged** (keeps its server-URL field;
  still cross-origin, still uses CORS).
- **Vite HMR through the gateway (dev):** configure `server.hmr` (`clientPort`/`host` to the
  gateway) and `server.allowedHosts` so the HMR websocket connects via the gateway origin.
  **Verify at execution;** documented fallback if fiddly: hit the Vite dev server directly
  in dev (frontend code stays identical because it uses relative `/api` + a Vite `/api`
  proxy) while the gateway remains the prod front door.

### 4. Web production container (static nginx)

- Serve the built SPA from nginx with SPA fallback: `try_files $uri $uri/ /index.html;`.
- Provide a Dockerfile: stage 1 Node (`npm ci` + `vite build`) → `dist`; stage 2 nginx
  copying `dist` into the web root + a small `nginx.conf`. **Verify at execution:** whether
  Aspire 13 can publish `AddViteApp` as a static-serving container directly, or we point the
  published `web` service at this Dockerfile.

### 5. API production hardening (`BookmarkFeeder.WebApi/Program.cs`)

- **No SPA/static serving in the API** - that is the web container's job. Remove any
  wwwroot/fallback plans; the API is purely `/api` + health + OpenAPI/Scalar.
- Add `app.UseForwardedHeaders()` (X-Forwarded-For/Proto) early so the API sees the real
  scheme/host from the gateway.
- Make `UseHttpsRedirection` **opt-in** via config (default off; the container runs plain
  HTTP behind the gateway) - replaces the current `!IsDevelopment()` guard which would
  break inside a plain-HTTP container.
- `ASPNETCORE_ENVIRONMENT=Production`, `ASPNETCORE_URLS=http://+:8080`. Connection string
  from `ConnectionStrings__bookmarkfeeder`; API key from `Authentication__ApiKey` (fail-fast
  already enforces it). Migrations run at startup via existing `InitializeDatabaseAsync`.
- **CORS**: no longer needed for the web app (same origin via gateway). Keep the existing
  permissive policy solely for the browser extension (cross-origin), or scope it to the
  extension - either is fine.

### 6. Production health & readiness

- `MapDefaultEndpoints` (`BookmarkFeeder.ServiceDefaults/Extensions.cs`) currently maps
  `/health` + `/alive` only in Development. Map them in **all** environments. `/health` =
  readiness (includes `AddDbContextCheck`); `/alive` = liveness. Compose healthchecks use
  `/alive`; the gateway exposes its own `/alive`.

### 7. Per-endpoint rate limiting (`BookmarkFeeder.WebApi`)

- `builder.Services.AddRateLimiter(...)` + `app.UseRateLimiter()`. Named fixed-window
  policies (starting values, tunable): `sync` ≈ 5/min (`/api/bookmarks/batch`), `writes`
  ≈ 100/min (mutating routes), `reads`/default ≈ 200/min. Partition by the `X-API-Key`
  header (fallback to `X-Forwarded-For`/remote IP).
- Apply via `.RequireRateLimiting("<policy>")` on the endpoint groups; `OnRejected` returns
  `429` with `Retry-After`.

### 8. Testcontainers integration tests (`BookmarkFeeder.WebApi.Tests`)

- Add `Testcontainers.PostgreSql`; a Postgres-backed fixture (`IAsyncLifetime` collection
  fixture) starts a container, runs `MigrateAsync`, and points a `WebApplicationFactory`
  API at it.
- Cover the InMemory-blind behaviors: unique `Url` index (duplicate + soft-deleted de-dup),
  `ILIKE` search (`search=`), soft-delete×unique interaction, cascade/`SetNull` on
  tag/category delete.
- Keep the fast InMemory suite; categorize the Postgres suite so it can be filtered out when
  Docker is unavailable.

### 9. Aspire publish → Docker Compose

- Add a compose publishing environment to the AppHost and the required package. **Verify at
  execution:** the Aspire 13.4.6 compose-publish package/API (`AddDockerComposeEnvironment`
  style) and publish command; custom Dockerfile hookup for `web` (nginx) and `api`; and that
  only `gateway` maps a host port. Postgres publishes with a named volume; API key + DB
  password surface as compose env/`.env`.

## External Dependencies (Conditional)

- **Yarp.ReverseProxy** - The reverse-proxy gateway.
  - **Justification:** single external origin + path routing without CORS or frontend
    rewrites; .NET/Aspire-native so it runs in dev and publishes to compose identically.
- **Testcontainers.PostgreSql** - Disposable PostgreSQL for integration tests.
  - **Justification:** validates unique-index / `ILIKE` / soft-delete behavior EF InMemory
    cannot.
- **Aspire compose-publishing package** (expected `Aspire.Hosting.Docker`, version to match
  Aspire 13.4.6) - Emits the Docker Compose artifacts.
  - **Justification:** the chosen approach generates compose from the AppHost. Exact id/version
    confirmed at execution.
- Rate limiting uses built-in `Microsoft.AspNetCore.RateLimiting` - no new package.

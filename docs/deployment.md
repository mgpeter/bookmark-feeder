# Self-hosting with Docker Compose

BookmarkFeeder deploys as four containers behind a single reverse-proxy gateway:

```
            gateway (YARP, the only exposed port)
              ├─ /api/{**} → webapi (.NET)
              └─ /{**}     → web (static nginx)
                             webapi → postgres
```

Only the **gateway** publishes a host port; `webapi`, `web`, and `postgres` are internal.
TLS is not included — run the gateway behind your NAS's own reverse proxy (e.g. Nginx
Proxy Manager / Traefik) if you want HTTPS.

## 1. Generate the compose artifacts

From the repo root (requires the Aspire CLI: `dotnet tool install -g aspire.cli`):

```bash
aspire publish --project BookmarkFeeder.AppHost/BookmarkFeeder.AppHost.csproj -o ./publish
```

This writes `publish/docker-compose.yaml` and `publish/.env`. **`publish/.env` contains
secrets** (the API key and a generated Postgres password) — do not commit it.

## 2. Configure `publish/.env`

- `API_KEY` — the shared key the web app and browser extension send as `X-API-Key`. Set a
  strong value.
- `POSTGRES_PASSWORD` — the database password (generated; keep or change).
- `GATEWAY_PORT` / `WEBAPI_PORT` — internal container ports. To pin the public port, edit the
  gateway's `ports:` in `docker-compose.yaml` to e.g. `"8080:${GATEWAY_PORT}"`.
- `GATEWAY_IMAGE` / `WEBAPI_IMAGE` / `WEB_IMAGE` — image tags (see step 3).

## 3. Build the images and start

The compose references pre-built images. Build them (Aspire can build + tag as part of
`aspire deploy`, or build manually), then:

```bash
cd publish
docker compose up -d
```

- The `webapi` container applies EF Core migrations on startup (and seeds only in
  Development), so the schema is created automatically.
- `postgres` data persists in a named Docker volume across restarts.

## 4. Use it

- Open the gateway's mapped port in a browser. The React app loads (served via the gateway
  from the `web` container); `/api/*` is routed to `webapi`.
- On first load you're taken to **Settings** — enter your `API_KEY`. It's stored in the
  browser and sent on every request. There is no separate API URL to configure (the app is
  same-origin behind the gateway).
- Point the **browser extension** at `http(s)://<host>:<port>/api` with the same key.

## Health & operations

- `/health` (readiness, includes the DB check) and `/alive` (liveness) are exposed on the
  API in all environments for container/orchestrator probes.
- Per-endpoint rate limits are enforced (429 + `Retry-After`), tunable via
  `RateLimiting:{Sync,Writes,Reads}` environment variables on the `webapi` service.
- The generated compose also includes an optional Aspire dashboard container for telemetry;
  remove it if you don't want it.

# Spec Tasks

- [x] 1. Pin what production runs
  - [x] 1.1 **Chose 18.3, and moved the tests up to it — not the reverse.** The dev data volume is
        already a PostgreSQL **18** cluster (`PG_VERSION = 18`, 64 MB, the 434 real bookmarks). A PG17
        container cannot read a PG18 data directory, so pinning to the tests' `17-alpine` would have
        broken the dev environment on the next AppHost run
  - [x] 1.2 `.WithImageTag("18.3")` in the AppHost; `PostgresApiFactory` moved from `postgres:17-alpine`
        to `postgres:18.3` — the same image production runs, so the suite now tests what ships
  - [x] 1.3 138/138 green against 18.3. This mattered: the entire tsvector / GIN / generated-column
        stack had only ever been tested on PG17 while dev and prod ran PG18 — that parity was an
        untested assumption until now

  **Found and fixed beyond the spec — unstable data volume name.** `WithDataVolume()` derives a hashed
  name we don't control, and it **demonstrably differs between contexts**: the dev run produced
  `bookmarkfeeder.apphost-c707ae991a-postgres-data` while `aspire publish` emitted
  `bookmarkfeeder.apphost-190750286b-postgres-data`. On a NAS that is a silent data-loss trap — a
  shifted hash means `docker compose up -d` creates a fresh empty volume and the collection appears to
  vanish (still on disk, under a name nothing references). Now pinned to
  `WithDataVolume("bookmarkfeeder-postgres-data")`. The existing dev cluster was copied into the new
  volume (64 MB, PG_VERSION verified); the old volume is left intact as a fallback.

- [ ] 2. Make `aspire publish` emit a runnable artifact
  - [ ] 2.1 Add the Docker Hub container registry + per-service image push options and
        `ContainerTargetPlatform.LinuxAmd64` build options in the AppHost
  - [ ] 2.2 `PublishAsDockerComposeService` / `ConfigureComposeFile`: fixed `8080:8080` gateway
        mapping, `restart: unless-stopped` on all services, `pg_isready` healthcheck on postgres +
        `service_healthy` gate on webapi, dashboard pinned to a non-nightly tag and a fixed port
  - [ ] 2.3 Replace the auto-generated Postgres password with an explicit `postgres-password`
        parameter so the value is stable across publishes; set both secrets via user-secrets
  - [ ] 2.4 Run `aspire publish -o ./publish` (PowerShell) and **read the emitted YAML + .env**:
        image names resolved, no random ports, restart policies present, versions pinned
  - [ ] 2.5 Confirm the repeatability claim by hand: fill a value in `.env`, re-run `aspire publish`,
        and verify it was **not** clobbered (`onlyIfMissing`)

- [ ] 3. Build and push the three images
  - [ ] 3.1 Stop the AppHost (it locks `BookmarkFeeder.WebApi.exe`), pick the tag scheme
        (`<git-short-sha>` + `latest`), `docker login -u mgpeter`
  - [ ] 3.2 Build + push `webapi` and `gateway` via `dotnet publish /t:PublishContainer`
        (`--os linux --arch x64`)
  - [ ] 3.3 Build + push `web` via its Dockerfile (`docker buildx build --platform linux/amd64 --push`)
  - [ ] 3.4 **Before the first public push**, grep the built web bundle for the API key and confirm no
        secret is baked (expected clean — Vite ignores `.env.development` in a production build, but a
        public image makes the cost of being wrong permanent)
  - [ ] 3.5 Verify each image is pullable and is `linux/amd64` (`docker manifest inspect`)

- [ ] 4. Deploy to the NAS and prove it ← *the point*
  - [ ] 4.1 Copy `publish/docker-compose.yaml` to the NAS; create `.env` there once (API key, DB
        password, image tags, `GATEWAY_PORT=8080`), `chmod 600`
  - [ ] 4.2 `docker compose up -d`; confirm containers reach healthy and **EF migrations applied on a
        fresh volume**
  - [ ] 4.3 Load `http://<nas>:8080` from a *different* machine on the LAN: the React app loads through
        the gateway and `/api` is routed behind it
  - [ ] 4.4 Confirm only the intended ports are published (gateway + dashboard), and that data survives
        `docker compose down && docker compose up -d`
  - [ ] 4.5 Point the browser extension at `http://<nas>:8080/api`, Test Connection, and sync — the
        same end-to-end path that was proven against the dev gateway

- [ ] 5. Doc truth
  - [ ] 5.1 Rewrite `docs/deployment.md`: real commands, the publish-emits-keys-only behaviour, image
        build/push, the NAS-resident `.env`. Remove the false claim that `.env` "contains secrets"
  - [ ] 5.2 Check off roadmap Phase 2's six built-but-unchecked items; mark Phase 1's actual state
  - [ ] 5.3 Fix `tech-stack.md`'s stale `asset_hosting: Served by the API` (superseded by DEC-007 — the
        `web` nginx container serves assets); add the registry/image facts
  - [ ] 5.4 Index this spec in `docs/specs/README.md` and move it to Completed

## Notes

- **No new dependencies.** The registry/build/push APIs are in the already-referenced
  `Aspire.Hosting` core; `Aspire.Hosting.Docker` 13.4.6 is already referenced.
- **Task 4 needs you.** I cannot reach the NAS: it needs SSH/file access and a `docker login`. I can
  do 1–3 and verify the artifacts; 4 is a joint session.
- **The bar is a running deployment, not a generated file.** The predecessor spec
  (`2026-07-09-production-deployment`) was marked Completed on "verified by inspection" — the compose
  was read, never run — which is why a file that cannot start sat in `publish/` for a week. This spec
  is done when the app loads from the NAS in a browser.
- **Deliberately not automated.** No CI/CD, no deploy script beyond documented commands: the flow runs
  a few times a month by one person, and a correct runbook beats a pipeline that hides what it does.
  Revisit if it becomes tedious.
- ⚠️ Several Aspire behaviours here come from **decompiling** `Aspire.Hosting.Docker` 13.4.6, not from
  docs. They are marked "verify" in the technical spec and must be confirmed by running the commands
  and reading the output.

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

- [x] 2. Make `aspire publish` emit a runnable artifact
  - [x] 2.1 **Registry/push/build APIs deliberately NOT used.** The compiler rejected every one as
        evaluation-only (`ASPIRECOMPUTE003` on `AddContainerRegistry`/`WithContainerRegistry`,
        `ASPIREPIPELINES003` on `WithImagePushOptions`/`WithContainerBuildOptions`/
        `ContainerTargetPlatform`) — *"subject to change or removal in future updates"*. Using them
        needs `NoWarn` and pins the deploy to APIs Microsoft may remove. They also buy nothing here:
        compose already emits `image: "${WEBAPI_IMAGE}"` with no registry configured, so image refs
        live in the NAS `.env` (fill-once, see 2.5) and the build commands pin `--arch x64` themselves
  - [x] 2.2 `ConfigureComposeFile` + `PublishAsDockerComposeService` (both non-experimental):
        `restart: unless-stopped` on all five services; `pg_isready` healthcheck on postgres +
        `condition: service_healthy` on webapi (was `service_started` — the API could race a
        cold-booting NAS); dashboard pinned to `aspire-dashboard:9.5.2` **stable** (Aspire emits the
        *nightly* repo on a floating `:latest` — a preview channel that can change under the NAS at
        any pull; both repos top out at 9.5.2, there is no 13.x dashboard) and its port pinned
        (`18888` alone published to a **random** host port)
  - [x] 2.3 Explicit `postgres-password` parameter replaces `AddPostgres`'s auto-generated one: a
        generated password is a new value per publish, so the volume would keep the first while a
        later `.env` carried another, and the API could never connect again
  - [x] 2.4 `aspire publish` re-run and the artifact **read**: dashboard `9.5.2` + `18888:18888`;
        postgres `18.3` + `bookmarkfeeder-postgres-data` + healthcheck; webapi `service_healthy`;
        gateway `"${GATEWAY_PORT}:${GATEWAY_PORT}"`; `restart: unless-stopped` throughout
  - [x] 2.5 **`onlyIfMissing` confirmed empirically** — set `GATEWAY_PORT=8080` in `.env`, re-ran
        `aspire publish` twice, value survived. This is the mechanism that makes the NAS `.env`
        fill-once, and it is now proven rather than inferred from decompiled code

  **Gateway port is `"${GATEWAY_PORT}:${GATEWAY_PORT}"`, not a hardcoded `8080:8080`.** The container's
  own `HTTP_PORTS` is derived from the same variable, so hardcoding the host side alone would let the
  two drift and the gateway would listen on a port nothing published. One knob, no drift.
  **`ConfigureEnvFile` is not usable for this**: it runs in the *prepare* phase (deploy), while publish
  always writes keys-only — values cannot be seeded from code at publish time.
  138/138 backend tests green; `publish/` remains gitignored.

- [x] 3. Build and push the three images
  - [x] 3.1 Tag scheme became **semver from a `VERSION` file**, not the planned git sha —
        `scripts/docker-{build,release}.{ps1,sh}` mirror the glance-dashboard pattern. `VERSION` is
        written only after a successful push, so a failed release never skips a number
  - [x] 3.2 `webapi` + `gateway` via `dotnet publish -t:PublishContainer --os linux --arch x64`
        (**`-t:` not `/t:`** — Git Bash rewrites `/t:` into a Windows path and MSBuild reports the
        useless *"MSB1008: Only one project can be specified"*)
  - [x] 3.3 `web` via its Dockerfile (`docker buildx build --platform linux/amd64`)
  - [x] 3.4 Images audited before the first public push: **clean** — no API key in the web bundle, no
        `VITE_` values baked, no `.env` shipped, `appsettings.json` carries `"ApiKey": ""`. Verified by
        opening the images, not by reasoning about them
  - [x] 3.5 All three confirmed `linux/amd64` and pullable. **0.1.0 is broken** (predates the task-4
        fixes); **0.1.1 verified good** by running the Hub images from a wiped data dir

- [x] 4. Deploy to the NAS and prove it ← *the point*
  - [x] 4.1 The artifact moved to a committed `docker/` directory (compose + `.env.*.template`) rather
        than the gitignored `publish/`; NAS `.env` filled once and never overwritten
  - [x] 4.2 Verified locally against a wiped data dir, then **on the NAS by the user**: containers
        healthy, `postgres` gates `webapi` via `service_healthy`, migrations applied automatically
  - [x] 4.3 **Running on the NAS** — the user confirmed the app loads. Port defaulted to **8081**, not
        8080, which is taken by their Glance dashboard
  - [x] 4.4 Data proven to survive a full teardown: bookmark created → `docker compose down`
        (containers destroyed) → `up` → still there. Only gateway + dashboard publish ports
  - [ ] 4.5 ⚠️ **Extension against the NAS not yet done** — the sync path is proven against the dev
        gateway, not `http://<nas>:8081/api`. Belongs with the extension distribution work

- [x] 5. Doc truth
  - [x] 5.1 `docs/deployment.md` rewritten around the flow that actually works, replacing one that
        could not run (it claimed `.env` "contains secrets" when publish writes blank keys, and
        hand-waved the images entirely). Troubleshooting covers the failures actually hit
  - [x] 5.2 Roadmap Phase 2 checked off; phases trued up against what shipped
  - [x] 5.3 `tech-stack.md`'s stale `asset_hosting` fixed; registry/deploy facts added
  - [x] 5.4 Indexed in `docs/specs/README.md`

  **Three bugs found by deploying that no amount of reading the compose would have caught, and which
  the predecessor spec's "verified by inspection" sign-off missed:**
  1. **Migrations never ran in any container.** `IsDesignTimeBuild()` treated
     `DOTNET_RUNNING_IN_CONTAINER=true` — set by *every* .NET container image — as a design-time
     build, so `InitializeDatabaseAsync` was skipped and every request died on
     `3D000: database "bookmarkfeeder" does not exist`. Latent since the first API commit.
  2. **Every page load 502'd.** nginx listened on its default 80; Aspire fixes the web resource at
     8000 and points the gateway at `http://web:8000`.
  3. **The data volume name was a moving target.** `WithDataVolume()` derives a hash that differed
     between `aspire run` and `aspire publish`, and compose prefixes it with the *directory* name —
     so the database's identity depended on what the folder was called. Now pinned, with an explicit
     compose project name.

  **Also beyond spec:** the cluster writes to a **bind mount** (`POSTGRES_DATA_PATH`, default
  `./data/postgres`) rather than a named volume. A named volume survives restarts fine, but lives in
  `/volume1/@docker/volumes` — invisible in File Station and not a folder Hyper Backup can select.
  That directory is now the only thing worth backing up.

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

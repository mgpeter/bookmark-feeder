# Specs

Index of feature specs. Each folder holds `spec.md` (requirements), `sub-specs/` (technical,
API, schema) and - once broken down for execution - `tasks.md`.

**Status vocabulary**

| Status | Meaning |
|---|---|
| `Planning` | Agreed but not started. Usually no `tasks.md`; sometimes broken down and queued to execute. |
| `In Progress` | Execution has begun - at least one parent task is checked off. |
| `Completed` | Every parent task in `tasks.md` is checked off and verified. |

The authoritative status of a spec is the `> Status:` line in its own `spec.md`; this table is a
convenience index. Per-task detail - including deviations, trade-offs and known gaps - lives in
each spec's `tasks.md`.

## In progress

Nothing in flight.

## Planned

Written, not yet broken into tasks.

| Spec | What it adds | Notes |
|---|---|---|
| [extension-auto-sync](2026-07-14-extension-auto-sync/) | MV3 service worker syncing on a `chrome.alarms` schedule, badge + error state | Groundwork done: `src/lib/sync.ts` is UI-free so the worker can import `runSync()` as-is, and a re-sync is proven to be a safe no-op (0 created / 260 skipped). The favicon queue/worker is the server-side counterpart pattern |
| [extension-publishing](2026-07-14-extension-publishing/) | Store submission: release pipeline, narrowed host permissions, assets, privacy policy | **Partly delivered already**: `npm run zip` exists (built during extension-redesign-sync task 4.1), and `icons/ai.png` is the 1024px master for promo art. Still outstanding: narrowing `host_permissions` from `https://*/*` to the configured origin, store assets, privacy policy, submission runbook. Best done *after* auto-sync, to avoid shipping twice |
| [import-export](2026-07-14-import-export/) | Netscape/Pocket import; JSON/HTML/CSV export | Least urgent now the extension is the browser-import path; the real value is Pocket migration and data portability |

## Completed

| Spec | Completed | Delivered |
|---|---|---|
| [nas-deploy-pipeline](2026-07-15-nas-deploy-pipeline/) | 2026-07-15 | **BookmarkFeeder actually runs on the NAS.** Versioned build/release scripts → Docker Hub, compose generated from the AppHost into a committed `docker/`, secrets in a fill-once `.env`, Postgres on a bind mount. Deploying found three bugs no amount of reading the YAML would have: migrations skipped in *every* container, nginx on the wrong port, and a data-volume name that depended on the folder name |
| [favicon-enrichment](2026-07-14-favicon-enrichment/) | 2026-07-14 | The project's first `IHostedService`: a bounded channel queue + background worker resolving each site's favicon from its **own origin only** (never a third-party service), 4 at a time with a politeness delay. Enqueued on create/sync, backfilled on startup. `Favicon:Enabled` turns outbound fetching off. ⚠️ Not yet run against the 434 real bookmarks |
| [full-text-search](2026-07-14-full-text-search/) | 2026-07-14 | Weighted `tsvector` + GIN index, ranked search replacing the `ILIKE` scan, tag/category facets, saved searches, and the UI (relevance sort, `<mark>` highlighting, facet panel). On real data: `deployed` went 0 → 9 hits, `testing` 3 → 8 |
| [mark-all-as-read](2026-07-14-mark-all-as-read/) | 2026-07-14 | `POST /api/bookmarks/mark-read` over a shared filter composition, marking every match across all pages in one `ExecuteUpdate`, behind a confirmation dialog stating the count |
| [extension-redesign-sync](2026-07-14-extension-redesign-sync/) | 2026-07-14 | Vite/React/shadcn popup matching the web app; sync ported to typed modules; verified end-to-end in Chrome **and** Edge through the gateway (434 bookmarks synced, 0 duplicates) |
| [production-deployment](2026-07-09-production-deployment/) | 2026-07-14 | YARP gateway, Aspire Docker Compose publishing, rate limiting, Testcontainers suite |
| [database-infrastructure](2025-08-15-database-infrastructure/) | 2026-07-08 | EF Core model, migrations, seed data, DbContext factory |

## Known issues

Not spec-scoped; each needs its own change.

- ⚠️ **Flaky integration suite (~1 full run in 8).** `SearchVectorTests` fails intermittently with no
  assertion message (an exception, not a bad expectation), never in isolation - only when test
  classes run in parallel, each spinning up its own PostgreSQL container. Seen again during
  favicon-enrichment and re-confirmed as pre-existing (3 consecutive clean runs after). Suggested
  fix: one xUnit collection with `ICollectionFixture<PostgresApiFactory>` so they share a container.
  Detail in [full-text-search/tasks.md](2026-07-14-full-text-search/tasks.md).
- ⚠️ **`Microsoft.OpenApi` 2.0.0 has a known high-severity advisory** ([GHSA-v5pm-xwqc-g5wc](https://github.com/advisories/GHSA-v5pm-xwqc-g5wc)),
  surfacing as `NU1903` on every build. Transitive via the OpenAPI/Scalar setup.
- **Nested `sourceFolder` paths are unverified by a live sync.** Both folders synced from real
  browsers were flat; nesting is covered only by unit tests and seeded rows.
- ~~BookmarkFeeder has never actually been deployed.~~ **Deployed 2026-07-15** and running on the NAS.
  The lesson stands though: `production-deployment` was signed off on *"verified by inspection"* and
  left a compose that could not start, plus three bugs that only surfaced when something actually ran.
  **Inspection is not verification.**
- ~~The production Postgres version is unpinned.~~ **Fixed** - pinned to `18.3` and the tests moved
  from `17-alpine` to match, so the suite tests the version that ships. Volume name and compose
  project name pinned too; both previously depended on a hash or a folder name.
- ⚠️ **The extension has not been pointed at the NAS.** Sync is proven against the dev gateway, not
  `http://<nas>:8081/api` - the last unverified link in the chain.
- **Favicon enrichment has never run against the real collection.** All 434 bookmarks still have
  `faviconUrl: null`; the backfill queues them on the next AppHost start. Until then, resolution
  rates and the politeness limits are only proven against stubs.

# Specs

Index of feature specs. Each folder holds `spec.md` (requirements), `sub-specs/` (technical,
API, schema) and — once broken down for execution — `tasks.md`.

**Status vocabulary**

| Status | Meaning |
|---|---|
| `Planning` | Written and agreed, but not yet broken into tasks. `tasks.md` does not exist. |
| `In Progress` | `tasks.md` exists and some parent tasks are checked off. |
| `Completed` | Every parent task in `tasks.md` is checked off and verified. |

The authoritative status of a spec is the `> Status:` line in its own `spec.md`; this table is a
convenience index. Per-task detail — including deviations and known gaps — lives in `tasks.md`.

## In progress

| Spec | Tasks | Next up |
|---|---|---|
| [full-text-search](2026-07-14-full-text-search/) | 1 / 5 | Task 2 — ranked search replacing the `ILIKE` block in `GetBookmarksAsync` |

## Planned

Written, not yet broken into tasks.

| Spec | What it adds | Notes |
|---|---|---|
| [extension-auto-sync](2026-07-14-extension-auto-sync/) | MV3 service worker syncing on a `chrome.alarms` schedule, badge + error state | Groundwork done: `src/lib/sync.ts` is UI-free so the worker can import `runSync()` as-is |
| [extension-publishing](2026-07-14-extension-publishing/) | Store submission: release pipeline, narrowed host permissions, assets, privacy policy | Best done *after* auto-sync, to avoid shipping twice. `npm run zip` already exists; `icons/ai.png` is the 1024px master for promo art |
| [favicon-enrichment](2026-07-14-favicon-enrichment/) | Background `IHostedService` resolving each site's favicon into `FaviconUrl` | The project's first hosted worker. Synced bookmarks currently have `faviconUrl: null` |
| [import-export](2026-07-14-import-export/) | Netscape/Pocket import; JSON/HTML/CSV export | Less urgent now the extension is the browser-import path; real value is Pocket migration and data portability |

## Completed

| Spec | Completed | Delivered |
|---|---|---|
| [extension-redesign-sync](2026-07-14-extension-redesign-sync/) | 2026-07-14 | Vite/React/shadcn popup matching the web app; sync ported to typed modules; verified end-to-end in Chrome **and** Edge through the gateway |
| [production-deployment](2026-07-09-production-deployment/) | 2026-07-14 | YARP gateway, Aspire Docker Compose publishing, rate limiting, Testcontainers suite |
| [database-infrastructure](2025-08-15-database-infrastructure/) | 2026-07-08 | EF Core model, migrations, seed data, DbContext factory |

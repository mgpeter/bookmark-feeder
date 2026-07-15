# Technical Specification

This is the technical specification for the spec detailed in @docs/specs/2026-07-14-extension-auto-sync/spec.md

## Technical Requirements

### 1. Service worker (BookmarkFeeder.BrowserExtension)

- Add a background **service worker** entry (`src/background/service-worker.ts`), registered in the
  manifest as `background.service_worker` and bundled by the existing `@crxjs/vite-plugin` build.
- Add the **`alarms`** permission to `manifest.json`.
- The worker owns: alarm handling, bookmark-change listeners, running syncs, updating state + badge.
  It must be event-driven (MV3 workers are ephemeral) - no long-lived timers; use `chrome.alarms`.

### 2. Scheduled sync

- A settings field for the interval (minutes; e.g. 15/30/60, or "off"). On change, (re)create a
  `chrome.alarms` alarm (`periodInMinutes`). `chrome.alarms.onAlarm` → run a sync of the selected
  folders. Recreate the alarm on install/startup (`onInstalled`/`onStartup`) from stored settings.

### 3. Sync-on-change (optional, toggle in settings)

- When enabled, register `chrome.bookmarks.onCreated/onChanged/onMoved/onRemoved` listeners that
  **debounce** (e.g. coalesce a burst within ~5s) and then run a sync of the selected folders. Guard
  against loops and no-op when nothing is selected / not configured.

### 4. Shared sync module

- Extract the sync logic (from the redesign spec's hooks) into a framework-agnostic module
  (`src/lib/sync.ts`): read settings/folders from `chrome.storage`, traverse selected folders, POST
  to `${serverUrl}/bookmarks/batch` with `X-API-Key`, return the summary. Used by **both** the popup
  ("Sync Now") and the service worker. A simple mutex/flag prevents concurrent runs.

### 5. Sync state, badge, and errors

- Persist to `chrome.storage`: `lastSyncAt`, `lastSummary` (created/skipped/errors), `lastError`, and
  a `syncStatus` (`idle | syncing | ok | error`). The popup subscribes (via `chrome.storage.onChanged`)
  and renders it.
- **Action badge:** `chrome.action.setBadgeText`/`setBadgeBackgroundColor` - e.g. spinner-ish while
  syncing, a count or check on success, `!` on error. Clear on success.
- **Error handling:** `401` → set an error state prompting the user to fix the API key (and surface in
  the popup); network/5xx failures → retry with capped exponential backoff before giving up and
  recording `lastError`. Never throw out of the worker.

### 6. Testing

- Vitest with a `chrome.*` stub: the shared `sync` module (payload/summary, concurrency guard); the
  alarm handler (alarm → sync called); the debounced change handler (burst → single sync); state/badge
  updates on success vs. 401 vs. network error.

## External Dependencies

None beyond the extension's existing stack (the `alarms` permission is a manifest change, not a package).

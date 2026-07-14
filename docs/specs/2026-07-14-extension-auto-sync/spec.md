# Spec Requirements Document

> Spec: Extension Background Auto-sync
> Created: 2026-07-14
> Status: Planning

## Overview

Make the extension sync automatically in the background via an MV3 service worker — on a configurable
schedule (`chrome.alarms`) and optionally when bookmarks change — sharing one sync module with the
popup's manual button, and surfacing sync state/history with an action badge and robust error
handling.

## User Stories

### Set it and forget it

As a user, I want my selected folders to sync automatically on a schedule, so that my BookmarkFeeder
instance stays up to date without me clicking "Sync Now".

The user sets a sync interval in settings; a background service worker runs the sync on that schedule
(and optionally right after bookmarks change), updating a visible sync status.

### Know it's working (or not)

As a user, I want clear feedback on background sync, so that I notice if my key is wrong or the server
is unreachable rather than silently drifting out of date.

The toolbar icon shows a badge for status/errors, and the popup shows the last run, its summary, and
any error; a bad key or network failure surfaces a clear message.

## Spec Scope

1. **MV3 service worker** - A background service worker (bundled by the Vite build) registered in the
   manifest, coordinating scheduled and event-driven syncs.
2. **Scheduled sync** - `chrome.alarms` at a user-configurable interval (e.g. 15/30/60 min), set in
   settings; the manual button remains.
3. **Sync-on-change (optional)** - Debounced `chrome.bookmarks.onCreated/onChanged/onMoved/onRemoved`
   listeners trigger a sync of the selected folders when enabled.
4. **Shared sync module** - One sync implementation used by both the popup and the worker.
5. **Sync state, badge & error handling** - Persisted last-run/summary/error in `chrome.storage`, an
   action badge (`chrome.action.setBadgeText`), and 401 / network-failure handling with backoff.

## Out of Scope

- Incremental/delta sync and server-side reconciliation of renames/deletes (full re-sync of selected
  folders, relying on server-side `skipDuplicates`).
- The popup redesign/build (previous spec) and store publishing (next spec).

## Expected Deliverable

1. With an interval configured, the service worker syncs the selected folders automatically on
   schedule (verifiable with a short test interval) without opening the popup.
2. When sync-on-change is enabled, editing bookmarks triggers a (debounced) sync.
3. The toolbar badge and popup reflect the last sync's status/summary, and a bad API key or offline
   server produces a clear, non-silent error.

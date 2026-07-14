# Spec Requirements Document

> Spec: Extension Redesign & Sync Focus
> Created: 2026-07-14
> Status: Completed
> Completed: 2026-07-14

## Overview

Rebuild the browser extension's popup on a Vite + React + shadcn/ui build that shares the web app's
theme and components, refocusing it as a clean, sync-only tool (folder selection, settings, manual
sync with status) with an "Open dashboard" button that launches the web app in a new tab. This
establishes the modern build foundation the background service worker will later plug into.

## User Stories

### A sync tool that looks like the app

As a user, I want the extension popup to match the BookmarkFeeder web app's look and feel, so that
the two feel like one product instead of a mismatched neon popup.

The popup is rebuilt with React + shadcn/ui using the same theme tokens, fonts, and components as the
web app; it presents only what's needed to sync — the folders to sync, the connection settings, and a
sync button with status — and an "Open dashboard" button to jump to the full app.

### Get to the full app quickly

As a user, I want a one-click link from the extension to my BookmarkFeeder dashboard, so that I can
browse/manage bookmarks in the web app without hunting for the URL.

An "Open dashboard" button opens the web app (derived from the configured server URL) in a new tab.

## Spec Scope

1. **Vite + React + shadcn build** - Replace the plain HTML/JS popup with a Vite-built React popup
   (MV3-aware extension build) sharing the web app's shadcn theme tokens and UI components.
2. **Sync-focused popup UI** - Folder-tree selection, settings (server URL + API key + Test
   Connection), and a manual "Sync Now" with last-sync status/summary — no bookmark browsing.
3. **Open-dashboard link** - A button that opens the web app in a new tab via `chrome.tabs.create`,
   using the web app URL derived from the configured server URL.
4. **Ported sync logic** - The existing recursive folder traversal + batch upload + storage from
   `js/popup.js` reimplemented as React hooks (behavior preserved).
5. **Unpacked + zipped build** - A `dist/` unpacked extension plus a `.zip` output.

## Out of Scope

- Background/automatic sync and a service worker (next spec).
- Store submission, screenshots, privacy policy, and host-permission narrowing (publishing spec).
- Any bookmark browsing/management inside the extension.

## Expected Deliverable

1. Loading the unpacked `dist/` shows a popup that visually matches the web app (shadcn theme, Geist),
   with folder selection, settings, and a working manual "Sync Now" that reports a summary.
2. The "Open dashboard" button opens the web app in a new browser tab.
3. `npm run build` produces a loadable unpacked extension and a distributable `.zip`.

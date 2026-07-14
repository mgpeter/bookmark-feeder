# Technical Specification

This is the technical specification for the spec detailed in @docs/specs/2026-07-14-extension-redesign-sync/spec.md

## Technical Requirements

### 1. Vite MV3 extension build (BookmarkFeeder.BrowserExtension)

- Introduce a Vite + React + TypeScript build for the extension. Use an MV3-aware Vite plugin —
  **`@crxjs/vite-plugin`** (bundles `manifest.json`, popup, and — later — the service worker, with
  HMR). **Verify at execution:** current `@crxjs/vite-plugin` version + MV3/Vite compatibility.
- `manifest.json` becomes the build's source of truth (popup points at the built `index.html`);
  keep permissions `bookmarks`, `storage`, and existing `host_permissions` (narrowing is the
  publishing spec). Output → `dist/` (unpacked) + a `.zip` npm script.
- Migrate the project layout to `src/` (React) while keeping `icons/`.

### 2. Shared design system with the web app

- Reuse the web app's shadcn setup: copy its `src/index.css` theme tokens (radix-nova, Geist) and
  the needed `src/components/ui/` components (button, card, input, label, select, badge, sonner,
  checkbox, dialog) into the extension, plus Tailwind v4 (`@tailwindcss/vite`) and `cn`/`lib/utils`.
  Goal: pixel-consistent look with `BookmarkFeeder.Web`. (A shared package is possible later; copy
  for now to keep the extension self-contained.)
- Constrain popup width/height to a sensible size (e.g. ~380px) and support light/dark per the tokens.

### 3. Popup UI (React), sync-focused

- Views/sections: **Folders** (tree/list selection via `chrome.bookmarks.getTree`, persisted
  selection), **Sync** (manual "Sync Now" + last-sync time + last summary), **Settings** (server URL,
  API key [password], Test Connection), and an **Open dashboard** button.
- **Open dashboard:** compute the web app URL from the stored `serverUrl` by stripping a trailing
  `/api` (or a dedicated `webAppUrl` setting) and open it with `chrome.tabs.create({ url })`.
- **No bookmark browsing** — the extension only selects folders and syncs.

### 4. Port existing sync logic into hooks

- Reimplement `js/popup.js` behavior as typed React hooks/modules:
  - `chrome.bookmarks` recursive folder traversal building `sourceFolder` paths.
  - Batch POST to `${serverUrl}/bookmarks/batch` with `X-API-Key` (+ 401/`!ok` handling), returning
    the summary.
  - `chrome.storage.sync` for `selectedFolders`, `serverUrl`, `apiKey`, `lastSync` (+ future
    settings).
  - Test Connection: `GET ${serverUrl}/tags` with the key.
- Behavior is preserved; only the implementation/structure changes. This module is written so the
  next spec can share it between the popup and the service worker.

### 5. Testing

- Vitest + React Testing Library for the popup, mocking the `chrome.*` APIs (a small `chrome` stub):
  the URL/sync helpers (traversal → items, dashboard-URL derivation, batch payload shape) and a popup
  render/interaction test (folder toggle persists; Sync Now calls the batch fetch; Open dashboard
  calls `chrome.tabs.create`).

## External Dependencies

- **@crxjs/vite-plugin** - MV3-aware Vite bundling of the manifest, popup, and service worker with HMR.
  - **Justification:** the standard way to build a modern React/Vite MV3 extension; avoids hand-wiring
    manifest/asset paths. Pin the latest stable at implementation.
- React, react-dom, Vite, Tailwind v4, shadcn/ui deps (radix-ui, lucide-react, class-variance-authority,
  clsx, tailwind-merge), Vitest + Testing Library — mirroring `BookmarkFeeder.Web`.

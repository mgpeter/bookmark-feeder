# Technical Specification

This is the technical specification for the spec detailed in @docs/specs/2026-07-14-extension-publishing/spec.md

## Technical Requirements

### 1. Permission hardening (store-review critical)

- Remove the broad `host_permissions: ["https://*/*"]` (and the `http://localhost:*/*` entry stays
  dev-only or is dropped for release). Add **`optional_host_permissions`** and request the specific
  configured server origin at runtime with `chrome.permissions.request({ origins: [<origin>/*] })`
  when the user saves their server URL in settings; use `chrome.permissions.contains` to check and
  re-request if changed. Sync fails gracefully with a prompt if the origin isn't granted.
- Keep only the permissions actually used: `bookmarks`, `storage`, `alarms`.

### 2. Build & release pipeline

- npm scripts: `build` (Vite production build → `dist/`), `zip` (zip `dist/` → `release/bookmarkfeeder-<version>.zip`),
  and a version-bump helper that updates `manifest.json` + `package.json` in lockstep. Optionally a
  GitHub Action that runs build+zip and attaches the artifact to a release.
- Ensure the built `manifest.json` is store-valid (MV3, versioned, icons, no dev-only entries).

### 3. Store assets

- Icons (existing 16/48/128; add 128 store icon if needed), 1–3 screenshots of the popup + a synced
  web-app view, a small promo tile, and short/long descriptions. Store under `store/` in the extension.

### 4. Privacy policy

- A published privacy-policy page/markdown: the extension reads the user's bookmarks and sends them
  **only** to the user's own self-hosted BookmarkFeeder server (the configured URL); it stores
  settings/selection in `chrome.storage`; no data is sent to any third party or the developer. Link it
  from the store listings (host it in the repo/docs or the web app).

### 5. Submission runbook

- A `store/PUBLISHING.md` documenting: creating Chrome Web Store + Edge Add-ons developer accounts,
  uploading the `.zip`, filling listing/assets, the permission-justification text (why `bookmarks` /
  optional host access), the privacy-policy URL, and the review/submit steps.

## External Dependencies

None (packaging/asset work; no new runtime packages). A zipping dev-dependency (e.g. a small archiver
or the platform `zip`) may be used by the `zip` script.

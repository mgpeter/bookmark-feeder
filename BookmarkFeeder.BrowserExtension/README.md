# BookmarkFeeder Browser Extension

Chrome/Edge extension that syncs selected bookmark folders to your self-hosted BookmarkFeeder.

It is deliberately **sync-only** - it does not browse your collection. That's the web app's job, and
there's a button to open it.

## What it does

- Pick which bookmark folders to sync; the selection persists via `chrome.storage.sync`.
- **Sync Now** walks the chosen folders recursively, preserves each bookmark's folder path as
  `sourceFolder`, and uploads everything in **one** batch request.
- Duplicates are skipped server-side by URL, so re-syncing is safe and two browsers can feed one
  collection. Proven on real data: re-running Chrome's sync gave **0 created / 260 skipped**, and
  Edge then contributed **166 created / 261 skipped** into the same collection.
- Reports the last sync time and a created/skipped summary.

## Build and load it

**Prerequisites:** Node.js 22+.

```bash
cd BookmarkFeeder.BrowserExtension
npm install
npm run build          # -> dist/
```

Then in Chrome (`chrome://extensions`) or Edge (`edge://extensions`):

1. Turn on **Developer mode**
2. **Load unpacked** → select the **`dist/`** folder (not the repo folder - `dist/` is what the build
   produces)
3. Pin the extension and open the popup

After any code change, `npm run build` again and hit **reload** on the extension card. The popup runs
the built bundle, so it will not change until you do.

## Configure it

The popup opens on **Settings** until a server is set.

| Field | Value |
|---|---|
| Server URL | your API base URL, e.g. `http://localhost:5180/api` (dev) or `http://<host>:8081/api` |
| API key | the `X-API-Key` your server is configured with |

**Test connection** verifies both, then pick folders and hit **Sync Now**. The **Dashboard** button
opens the web app, derived from the server URL by dropping the trailing `/api`.

Settings live in `chrome.storage.sync`, which is **per browser profile** - each browser you install
into needs configuring once.

## Package for distribution

```bash
npm run zip            # builds, then writes bookmarkfeeder-1.0.0.zip
```

Named from `manifest.json`'s `name` + `version`, with `manifest.json` at the zip's root - what the
stores require.

## Development

```bash
npm test               # vitest - chrome.* APIs are stubbed, no network
npm run build          # tsc -b && vite build
```

Built with React 19 + Vite + Tailwind v4 + shadcn/ui (the same `radix-nova` theme and Geist font as
the web app, so the popup matches it), bundled for MV3 by
[`@crxjs/vite-plugin`](https://github.com/crxjs/chrome-extension-tools). `manifest.json` at the
project root is the build's source of truth.

```
manifest.json          # source of truth; popup -> built index.html
index.html, src/       # React popup
  lib/                 # sync, api, bookmarks, settings - UI-free on purpose, so the
                       # planned service worker can import runSync() unchanged
  features/            # folder picker, settings, sync panel
scripts/zip.mjs        # packaging
icons/                 # 16/48/128 + ai.png (1024px master)
dist/                  # build output - this is what you load unpacked
```

## Known gaps

- **Manual sync only.** Scheduled background sync is specced but not built
  (`docs/specs/2026-07-14-extension-auto-sync/`); `src/lib/sync.ts` is already UI-free so the service
  worker can reuse it.
- **Not on any store yet** - load unpacked, or see
  `docs/specs/2026-07-14-extension-publishing/`. That spec also covers narrowing
  `host_permissions` - currently `http://localhost:*/*` and `https://*/*`, i.e. every HTTPS site -
  down to just the server you configure. Worth doing before any store review.

## License

[MIT](../LICENSE), same as the rest of the repo.

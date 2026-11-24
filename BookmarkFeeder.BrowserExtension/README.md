# BookmarkFeeder Browser Extension

Chrome/Edge/Safari extension that syncs selected bookmark folders to your self-hosted BookmarkFeeder.

It is deliberately **sync-only** - it does not browse your collection. That's the web app's job, and
there's a button to open it.

## What it does

- Pick which bookmark folders to sync; the selection persists in `chrome.storage.local`.
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

## Safari

Safari runs the same popup code, wrapped in a small macOS container app (Safari only loads
extensions that ship inside an app). The Xcode project in `safari/` was generated once with Apple's
`safari-web-extension-packager` and **references** the `dist-safari/` build rather than copying it,
so a fresh build is all it needs - never regenerate it for a code change.

**Safari has no bookmarks API for extensions** (still true in Safari 27 - the popup's
`chrome.bookmarks` is simply missing). So the Safari build reads bookmarks natively: the popup sends
`{ type: "getTree" }` over `runtime.sendNativeMessage`, and the extension's Swift handler
(`safari/BookmarkFeeder/BookmarkFeeder Extension/SafariBookmarks.swift`) parses
`~/Library/Safari/Bookmarks.plist` and replies with a `chrome.bookmarks`-shaped tree. Everything
after that - folder picking, `sourceFolder` paths, the upload - is the shared code. Folder ids are
Safari's bookmark UUIDs, so the selection survives restarts. Safari bookmarks carry no "date added"
(only Reading List items do), so those are uploaded without one.

`vite build --mode safari` writes `dist-safari/` with a derived manifest: `nativeMessaging` added,
`bookmarks` dropped. The Chrome/Edge `dist/` is unchanged and never asks for native messaging.

**Prerequisites:** Xcode (full Xcode, not just the Command Line Tools), with its license accepted
(`sudo xcodebuild -license accept`). The scripts use `/Applications/Xcode.app` even when
`xcode-select` points at the Command Line Tools; set `DEVELOPER_DIR` to use another Xcode.

```bash
npm run safari:build   # tsc + vite --mode safari -> dist-safari/, then xcodebuild -> safari/build/
npm run safari:open    # launches the app once, which registers the extension with Safari
```

The build is unsigned (signed to run locally only), so Safari must be told to accept it:

1. Safari → **Settings → Advanced** → tick **Show features for web developers**
2. Safari → **Settings → Developer** → tick **Allow unsigned extensions**. Safari forgets this
   every time it quits, so repeat it after each restart
3. Safari → **Settings → Extensions** → enable **BookmarkFeeder Extension**
4. Grant it access to your server's site (**Edit Websites…** on the same page). Without that grant
   Safari blocks the requests and **Test connection** reports the server as unreachable
5. **Full Disk Access**, because `~/Library/Safari` is privacy-protected: System Settings →
   **Privacy & Security → Full Disk Access** → **+** → choose `BookmarkFeeder.app` (in
   `safari/build/Build/Products/Debug/`) and switch it on. The Folders tab explains this when the
   grant is missing. The extension's sandbox still only allows reading that one file, read-only

The ad-hoc signature changes on every build, and macOS ties the Full Disk Access grant to it - so
after a rebuild, remove BookmarkFeeder from the list and add it again. Signing with a free Apple ID
(Xcode → target → Signing & Capabilities → Team: your Personal Team) keeps the grant stable.

After a code change: `npm run safari:build`, then toggle the extension off and on in Safari's
settings. Settings live in Safari's own local extension storage, so configure it once like any
other browser. Signing, notarisation and the App Store are not set up yet - and the App Store would not
accept the read-only file exception the native handler relies on.

## Configure it

The popup opens on **Settings** until a server is set.

| Field | Value |
|---|---|
| Server URL | your API base URL, e.g. `http://localhost:5180/api` (dev) or `https://<machine>.<tailnet>.ts.net/api` |
| API key | a key you mint in the web app under **Settings → API keys** |

The key is **not** a server-wide secret any more: you sign in to the web app, create a key, and it
belongs to your account. It is displayed once, so paste it here straight away - only a hash is
stored, and a lost key can only be replaced.

Create one key per browser. Revoking a key signs out just that browser. A key can upload bookmarks
and read tags and categories; it cannot delete anything or manage keys, so one leaked from a
browser profile cannot damage your collection.

**Test connection** verifies both, then pick folders and hit **Sync Now**. The **Dashboard** button
opens the web app, derived from the server URL by dropping the trailing `/api`.

Settings live in `chrome.storage.local`: **this browser profile on this device only.** They are
deliberately not in `storage.sync`, which Chrome copies to every device on the same Google account -
that would share one API key between machines (so revoking it breaks them all) and carry folder
ids that mean different folders on another device. Each browser you install into needs configuring
once. Extension versions before 1.0.1 kept them in sync storage; the first popup open moves them over.

The extension never talks to Auth0, which is deliberate: an MV3 popup doing OAuth would need a
pinned extension id, a second Auth0 application and refresh-token handling, and would end up with
*more* authority than syncing requires. It also means the extension keeps working when the server
cannot reach Auth0, since key validation is entirely local. See DEC-008.

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
npm run safari:build   # build + Xcode wrapper (macOS, needs Xcode)
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
safari/                # Xcode wrapper app for Safari: native bookmarks reader; references dist-safari/
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

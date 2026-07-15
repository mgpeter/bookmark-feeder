# Spec Tasks

- [x] 1. Vite + React + shadcn extension build
  - [x] 1.1 Spike: `@crxjs/vite-plugin@2.7.1` declares `vite: ^3||^4||^5||^6||^7||^8` - Vite 8 supported (matches the web app's line)
  - [x] 1.2 Scaffolded Vite + React + TS with the crxjs plugin; `manifest.json` is the build source (popup → `index.html`); outputs `dist/`
  - [x] 1.3 Tailwind v4 + `shadcn init -t vite -b radix -p nova` → identical `radix-nova`/`neutral`/`lucide` config + theme tokens as the web app; added 10 `components/ui/*` + `lib/utils`; popup constrained to 380px
  - [x] 1.4 `npm run build` green (1881 modules); `dist/` is a valid loadable MV3 bundle (manifest + popup + assets + icons). In-browser side-load/visual lands in Task 4.2

- [x] 2. Port sync + storage into typed modules
  - [x] 2.1 Tests with an in-memory `chrome.*` stub (`src/test/chrome-stub.ts`): traversal → items, batch payload shape, dashboard-URL derivation, settings, sync orchestration - 26 tests
  - [x] 2.2 Ported the recursive traversal to `src/lib/bookmarks.ts` (`sourceFolder` paths); normalizes to the API's field limits and skips folders deleted since selection
  - [x] 2.3 Ported the batch POST to `src/lib/api.ts` (`/bookmarks/batch` + `X-API-Key`, 401/429/`!ok`/network handling, returns the summary) + `testConnection` (`GET /tags`)
  - [x] 2.4 Ported settings to `src/lib/settings.ts` (`chrome.storage.sync`, typed + defaulted); orchestration in `src/lib/sync.ts`
  - [x] 2.5 26/26 tests pass and the build stays green; modules are UI-free so the spec-#5 service worker can import them. `js/popup.js` deleted (fully ported)

- [x] 3. Popup UI - folders, settings, sync, dashboard link
  - [x] 3.1 Component tests in `src/App.test.tsx` (8): folder toggle persists both ways, Sync Now calls the batch fetch + shows the summary, failures surface, dashboard opens a tab, unconfigured lands on Settings, URL validation, key stays masked
  - [x] 3.2 `features/folder-picker.tsx` - folder list from `chrome.bookmarks.getTree`, depth-indented checkboxes in a `ScrollArea`, selection stored in tree order
  - [x] 3.3 `features/settings-panel.tsx` - server URL + password-masked API key + Test Connection (`GET {serverUrl}/tags`), with URL validation
  - [x] 3.4 `features/sync-panel.tsx` - Sync Now, last-sync time, created/skipped/failed summary badges, inline errors. No bookmark browsing
  - [x] 3.5 Dashboard button in the header → `chrome.tabs.create({ url: dashboardUrl(serverUrl) })`
  - [x] 3.6 34/34 tests pass and `npm run build` is green. Added jsdom polyfills for `matchMedia`/`ResizeObserver` (sonner + Radix need them)

- [x] 4. Package + end-to-end against the gateway
  - [x] 4.1 `npm run zip` → `scripts/zip.mjs` (archiver) builds then packages `dist/` contents into `bookmarkfeeder-<manifest version>.zip` (201.8 kB, `manifest.json` at the zip root); `*.zip` gitignored
  - [x] 4.2 Stack run via AppHost; `dist/` loaded unpacked in **Chrome** (id `kgiekdlblfmhmjjnphfgiogcmfjcenbj`) and in **Edge** - same unmodified bundle, no per-browser build needed
  - [x] 4.3 Configured `http://localhost:5180/api` + `dev-local-bookmarkfeeder-key`; Test Connection succeeded. CORS survives the YARP hop - preflight returns `204` with `Access-Control-Allow-Headers: content-type,x-api-key` for both `POST /api/bookmarks/batch` and `GET /api/tags`
  - [x] 4.4 Synced the real `Research` folder → **260 created** in a single request (8 → 268 bookmarks)
  - [x] 4.5 Dashboard shows the real bookmarks with `sourceFolder: Research`, original `dateAdded` (2023) and resolved favicons; 268 distinct URLs / 0 duplicate rows. Re-sync reported **0 created / 260 skipped**

  Also verified (second use case): syncing the equivalent folder from **Edge** created 166 / skipped 261 - the 261 being URLs Chrome had already synced (260) plus one matching a seeded URL. Result: 434 bookmarks, 0 duplicate URL rows, i.e. dedupe is by URL across the whole library, not per browser, so two browsers feed one collection safely. Settings don't carry across browsers (`chrome.storage.sync` is per profile) - each install is configured once.

  Still unexercised by a live sync: **nested `sourceFolder` paths**. Both real folders synced were flat; nesting is covered only by unit tests and the seeded rows.

  Fixed during this task: Save/Test connection were `disabled` on an empty server URL while the placeholder showed the exact value needed - the field read as filled and the buttons were an unexplained dead end. Now always clickable, reporting the problem instead. Regression test added.

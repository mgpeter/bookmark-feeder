# Spec Summary (Lite)

Rebuild the browser extension popup on a Vite + React + shadcn/ui build that shares the web app's
theme tokens and components, refocused as a sync-only tool: folder selection, settings (server URL +
API key + test-connection), and a manual "Sync Now" with last-sync status — plus an "Open dashboard"
button that opens the web app in a new tab. The existing recursive folder traversal + batch-upload
sync logic is ported into React hooks (behavior preserved), and the build emits an unpacked `dist/`
plus a distributable `.zip`. This modern build foundation is what the background service worker plugs
into next.

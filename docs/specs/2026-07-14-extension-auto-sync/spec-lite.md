# Spec Summary (Lite)

Add background auto-sync to the extension via an MV3 service worker: a `chrome.alarms` scheduled sync
at a user-configurable interval plus optional debounced sync-on-bookmark-change, sharing a single
sync module with the popup's manual "Sync Now". Persist sync state/history (last run, summary, error)
in `chrome.storage`, show status via an action badge, and handle 401 / network failures with backoff.
Keeps full re-sync of selected folders (server-side `skipDuplicates` dedups); incremental/delta sync
is out of scope.

# Spec Summary (Lite)

Add bookmark import and export. Import is a synchronous multipart upload that auto-detects and
parses browser bookmarks HTML (Netscape), Pocket/Instapaper HTML, JSON, and CSV - mapping folder
paths to `sourceFolder` and source tags to tags - then inserts through the existing batch path
(skipping URLs already present) and returns an imported/skipped/errors summary. Export downloads the
collection (optionally filtered) as JSON, HTML (Netscape, re-importable), or CSV. Exposed via API
endpoints under `/api/bookmarks` and Import/Export actions in the web UI.

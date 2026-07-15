# Spec Requirements Document

> Spec: Bookmark Import & Export
> Created: 2026-07-14
> Status: Planning

## Overview

Let users move their collection in and out of BookmarkFeeder: synchronously import from browser
bookmarks HTML (Netscape format) and Pocket/Instapaper exports - mapping folders to `sourceFolder`
and source tags to tags, skipping URLs already present - and export the collection as JSON, HTML
(re-importable), or CSV, via API endpoints and a web UI action.

## User Stories

### Bring my existing bookmarks in

As a new self-hoster, I want to import my browser's bookmarks export (and my Pocket/Instapaper
export), so that I can seed BookmarkFeeder with everything I already have without re-adding by hand.

The user picks their exported file in the web UI; the server detects the format, parses it (folders
become `sourceFolder`, source tags become tags), inserts everything through the existing batch path
(skipping duplicates by URL), and shows an imported / skipped / errors summary.

### Take my data with me

As a privacy-conscious user, I want to export my whole collection in open formats, so that I own my
data and can back it up or move it elsewhere at any time.

The user chooses a format (JSON, HTML, or CSV) and downloads a file of their collection (optionally
matching the current filters). The HTML export re-imports into browsers and back into BookmarkFeeder.

## Spec Scope

1. **Import endpoint** - A multipart upload that auto-detects and parses browser bookmarks HTML
   (Netscape), Pocket/Instapaper HTML, our JSON, and CSV.
2. **Import mapping + de-duplication** - Folder paths map to `sourceFolder`, source tags map to
   tags; insertion reuses the existing batch de-duplication (skip URLs already present) and returns
   an imported / skipped / errors summary.
3. **Export endpoint** - Download the collection (optionally filtered) as JSON, HTML (Netscape,
   re-importable), or CSV.
4. **Web UI actions** - An Import action (file picker → summary) and an Export action (choose format
   → download) in the bookmarks screen.

## Out of Scope

- Asynchronous/background import with progress polling (import runs synchronously).
- Mapping folders to tags (folders map to `sourceFolder`).
- Scheduled/automated exports and backup rotation (possible later enhancement).
- Importing per-item read-state or notes beyond what the source formats provide.

## Expected Deliverable

1. Uploading a browser bookmarks HTML export imports the bookmarks (folder paths → `sourceFolder`),
   skips URLs already present, and returns an imported / skipped / errors summary shown in the UI.
2. A Pocket/Instapaper export imports with its tags mapped to bookmark tags.
3. Export produces a downloadable JSON, HTML (re-importable), or CSV of the collection, optionally
   matching the active filters.

# Database Schema

This is the database schema implementation for the spec detailed in @docs/specs/2026-07-14-favicon-enrichment/spec.md

## Changes

### Bookmarks - add FaviconFetchedAt

- New column `FaviconFetchedAt` of type `timestamptz`, **nullable**.
- `FaviconUrl` already exists (`character varying(2048)`, nullable) - no change; it holds the
  resolved remote favicon URL.

```sql
ALTER TABLE "Bookmarks" ADD COLUMN "FaviconFetchedAt" timestamp with time zone NULL;
```

Rationale:
- Records when favicon discovery was last attempted (success or failure).
- Backfill enqueues only rows where `FaviconFetchedAt IS NULL`, and failed attempts set it, so a
  site with no discoverable favicon is not re-fetched on every startup. No index needed (backfill is
  a one-time scan; volumes are small for a personal collection).

## Migration

One EF migration adds `Bookmarks.FaviconFetchedAt`. Applied automatically at startup
(`InitializeDatabaseAsync`) and covered by the existing model/migration tests.

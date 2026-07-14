# Database Schema

This is the database schema implementation for the spec detailed in @docs/specs/2026-07-14-full-text-search/spec.md

## Changes

### Bookmarks — generated search vector + GIN index

- New **generated** column `SearchVector` of type `tsvector`, computed from the row's
  `Title` (weight A), `Description` (weight B, coalesced when null), and `Url` (weight C),
  in the `english` configuration. Stored/generated, so it stays in sync automatically and
  backfills existing rows on migration.
- New **GIN index** on `SearchVector` for fast `@@` matching and ranking.

Effective SQL (EF/Npgsql emits the equivalent via `HasGeneratedTsVectorColumn`):

```sql
ALTER TABLE "Bookmarks" ADD COLUMN "SearchVector" tsvector
    GENERATED ALWAYS AS (
        setweight(to_tsvector('english', coalesce("Title", '')), 'A') ||
        setweight(to_tsvector('english', coalesce("Description", '')), 'B') ||
        setweight(to_tsvector('english', coalesce("Url", '')), 'C')
    ) STORED;

CREATE INDEX "IX_Bookmarks_SearchVector" ON "Bookmarks" USING GIN ("SearchVector");
```

Rationale: replaces the unindexable `ILIKE '%term%'` scan with an index-backed, weighted,
relevance-rankable search. Tag names are matched separately in the query (not in this column),
per the chosen generated-column approach.

### New table — SavedSearches

| Column       | Type                        | Notes                                  |
|--------------|-----------------------------|----------------------------------------|
| Id           | uuid                        | PK                                     |
| Name         | varchar(200), not null      | Display name                           |
| Query        | varchar(2048), not null     | Serialized query string / filter params |
| DateCreated  | timestamptz, not null       | Set in app code (UTC)                  |

- Single-tenant (no user FK). Optional unique index on `Name` for tidy de-duplication
  (decide during implementation; not required).

## Migration

One EF migration adds the `Bookmarks.SearchVector` generated column + GIN index and creates the
`SavedSearches` table. Applied automatically at startup (`InitializeDatabaseAsync`) and validated
by the Testcontainers suite against real PostgreSQL.

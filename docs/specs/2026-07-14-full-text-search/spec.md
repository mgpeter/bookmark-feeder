# Spec Requirements Document

> Spec: Full-text Search & Discovery
> Created: 2026-07-14
> Status: Completed
> Completed: 2026-07-14

## Overview

Replace the `ILIKE` scan on `GET /api/bookmarks` with PostgreSQL full-text search — a
generated, GIN-indexed `tsvector` over title/description/url for ranked matching (with tag
names matched alongside) — returning relevance-ranked results and tag/category facet counts,
with the search term highlighted in the existing UI, plus saved searches to re-run a
query+filters combination.

## User Stories

### Fast, relevant search

As someone with a large bookmark collection, I want search results ranked by relevance and
backed by an index, so that the most useful matches come first and search stays fast as the
collection grows (instead of a leading-wildcard scan that can't use an index).

The user types a query in the existing search box; results re-order by relevance (matching
title, description, url, and tag names, case-insensitively), combined with any active filters
(tags, category, read state, source folder, dates) and pagination.

### Narrow down with highlights and facets

As a user refining a search, I want to see which terms matched and how results break down by
tag and category, so that I can quickly narrow to what I want.

Matched terms are highlighted in each result's title/description, and a facet panel shows the
tag and category counts for the current query; clicking a facet adds it as a filter.

### Save a search to re-run

As a user who repeats the same searches, I want to save a query with its filters and re-run it
later, so that I don't re-enter it each time.

## Spec Scope

1. **PostgreSQL FTS backend** - A generated, weighted `tsvector` column over title/description/url
   on Bookmarks with a GIN index, added via an EF migration.
2. **Ranked search on `GET /api/bookmarks`** - When `search` is present, match via `tsvector`
   (websearch query) OR tag-name match, order by relevance rank; keep all existing filters,
   sorting, and pagination working together.
3. **Facets** - Tag and category counts for the current search+filter query, returned in the
   paged response.
4. **Highlighting in the UI** - The search term(s) are highlighted (`<mark>`) in each result's
   title/description client-side.
5. **Saved searches** - A `SavedSearch` entity + CRUD API + a UI panel to save the current
   query/filters and re-run a saved one.

## Out of Scope

- A dedicated `/api/search` endpoint (search stays unified on `/api/bookmarks`).
- Trigger-maintained `tsvector` that ranks tag names (using the generated-column + separate
  tag-match approach instead).
- Semantic/AI search, fuzzy/typo tolerance (trigram), and autocomplete/suggestions.
- Server-side `ts_headline` highlighting (done client-side).
- Multi-user scoping of saved searches, search history/analytics, and result subscriptions.

## Expected Deliverable

1. Searching returns index-backed, relevance-ranked results matching title/description/url and
   tag names case-insensitively, combinable with the existing filters and pagination.
2. Results show the matched term highlighted, and a facet panel shows tag/category counts that
   refine the search when clicked.
3. A search + filter combination can be saved and re-run from the UI.

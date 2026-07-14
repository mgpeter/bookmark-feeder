# Spec Summary (Lite)

Replace the ILIKE scan on `GET /api/bookmarks` with PostgreSQL full-text search: a generated,
GIN-indexed tsvector over title/description/url for relevance-ranked matching (tag names matched
alongside), with tag/category facet counts in the response and client-side term highlighting in
the existing search UI. Also adds saved searches (a SavedSearch entity + CRUD API + UI) to
re-run a query and its filters. Search stays unified on the bookmarks endpoint and composes with
all existing filters, sorting, and pagination.

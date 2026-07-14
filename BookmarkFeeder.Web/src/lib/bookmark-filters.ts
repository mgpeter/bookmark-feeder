import type { BookmarkQuery } from '@/types/models'

/**
 * The BookmarkQuery fields that actually narrow the result set. Paging and sorting are
 * excluded deliberately: they change what you see, not which bookmarks match.
 */
const NARROWING_KEYS = [
  'search',
  'tags',
  'categories',
  'sourceFolder',
  'isRead',
  'dateFrom',
  'dateTo',
] as const satisfies readonly (keyof BookmarkQuery)[]

/** The subset of a query that narrows the set — what a bulk action should act on. */
export function narrowingFilters(query: BookmarkQuery): BookmarkQuery {
  const filters: Record<string, unknown> = {}
  for (const key of NARROWING_KEYS) {
    const value = query[key]
    // `false` is a real filter (unread), so only undefined/null/'' are treated as absent.
    if (value !== undefined && value !== null && value !== '') {
      filters[key] = value
    }
  }
  return filters as BookmarkQuery
}

/** Whether anything is narrowing the set — drives the bulk-action confirmation copy. */
export function hasNarrowingFilters(query: BookmarkQuery): boolean {
  return Object.keys(narrowingFilters(query)).length > 0
}

import { useSearchParams } from 'react-router-dom'
import { hasNarrowingFilters } from '@/lib/bookmark-filters'
import type { BookmarkQuery, SortBy, SortOrder } from '@/types/models'

export type ReadFilter = 'all' | 'read' | 'unread'
export type ViewMode = 'grid' | 'list'

/**
 * The page to move to when the requested one is past the end, or null when it is fine. Deleting the
 * last bookmark on the last page (or a stale link) otherwise strands you on an empty page, and the
 * pager that would get you back only renders when there is more than one page. Zero pages means an
 * empty result, where there is nowhere better to go.
 */
export function clampedPage(page: number, totalPages: number): number | null {
  return totalPages >= 1 && page > totalPages ? totalPages : null
}

/**
 * The URL params worth storing as a saved search, or '' when there is nothing to save. Where you
 * were (page) and how it was laid out (view) are not part of a search: applying one should start
 * at page 1 in whatever layout you are using. And a sort or a layout on its own is not a search.
 */
export function savedSearchString(params: URLSearchParams, query: BookmarkQuery): string {
  if (!hasNarrowingFilters(query)) return ''
  const saved = new URLSearchParams(params)
  saved.delete('page')
  saved.delete('view')
  return saved.toString()
}

export function useBookmarkQuery() {
  const [params, setParams] = useSearchParams()

  const read = params.get('read')
  const search = params.get('q') || undefined
  const query: BookmarkQuery = {
    page: Number(params.get('page')) || 1,
    pageSize: Number(params.get('pageSize')) || 20,
    search,
    tags: params.get('tags') || undefined,
    categories: params.get('categories') || undefined,
    sourceFolder: params.get('source') || undefined,
    dateFrom: params.get('from') || undefined,
    dateTo: params.get('to') || undefined,
    isRead: read === 'read' ? true : read === 'unread' ? false : undefined,
    // Searching ranks by relevance unless a sort is chosen; browsing is newest-first.
    // This must be explicit: the UI always sends a sortBy, so the API's own relevance
    // default would never get the chance to fire.
    sortBy: (params.get('sort') as SortBy | null) ?? (search ? 'relevance' : 'dateAdded'),
    sortOrder: (params.get('dir') as SortOrder | null) ?? 'desc',
  }

  const view: ViewMode = params.get('view') === 'list' ? 'list' : 'grid'
  const readFilter: ReadFilter =
    read === 'read' ? 'read' : read === 'unread' ? 'unread' : 'all'

  /** Merge params; empty/null clears the key. Resets page unless page is being set. */
  function patch(updates: Record<string, string | null>) {
    setParams(
      (prev) => {
        const next = new URLSearchParams(prev)
        for (const [key, value] of Object.entries(updates)) {
          if (value === null || value === '') next.delete(key)
          else next.set(key, value)
        }
        if (!('page' in updates)) next.delete('page')
        return next
      },
      { replace: true },
    )
  }

  return { query, params, patch, view, readFilter }
}

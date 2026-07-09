import { useSearchParams } from 'react-router-dom'
import type { BookmarkQuery, SortBy, SortOrder } from '@/types/models'

export type ReadFilter = 'all' | 'read' | 'unread'
export type ViewMode = 'grid' | 'list'

export function useBookmarkQuery() {
  const [params, setParams] = useSearchParams()

  const read = params.get('read')
  const query: BookmarkQuery = {
    page: Number(params.get('page')) || 1,
    pageSize: Number(params.get('pageSize')) || 20,
    search: params.get('q') || undefined,
    tags: params.get('tags') || undefined,
    categories: params.get('categories') || undefined,
    sourceFolder: params.get('source') || undefined,
    isRead: read === 'read' ? true : read === 'unread' ? false : undefined,
    sortBy: (params.get('sort') as SortBy | null) ?? 'dateAdded',
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

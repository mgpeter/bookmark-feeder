import {
  keepPreviousData,
  useMutation,
  useQuery,
  useQueryClient,
} from '@tanstack/react-query'
import { api } from '@/lib/api-client'
import { narrowingFilters } from '@/lib/bookmark-filters'
import type {
  Bookmark,
  BookmarkQuery,
  CreateBookmarkRequest,
  PagedResult,
  UpdateBookmarkRequest,
} from '@/types/models'

export const bookmarksKey = 'bookmarks'

export function useBookmarks(query: BookmarkQuery) {
  return useQuery({
    queryKey: [bookmarksKey, 'list', query],
    queryFn: () =>
      api.get<PagedResult<Bookmark>>('/bookmarks', query as Record<string, unknown>),
    placeholderData: keepPreviousData,
  })
}

export function useBookmark(id: string | undefined) {
  return useQuery({
    queryKey: [bookmarksKey, 'detail', id],
    queryFn: () => api.get<Bookmark>(`/bookmarks/${id}`),
    enabled: Boolean(id),
  })
}

export function useCreateBookmark() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: CreateBookmarkRequest) => api.post<Bookmark>('/bookmarks', body),
    onSuccess: () => qc.invalidateQueries({ queryKey: [bookmarksKey] }),
  })
}

export function useUpdateBookmark() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: UpdateBookmarkRequest }) =>
      api.put<Bookmark>(`/bookmarks/${id}`, body),
    onSuccess: () => qc.invalidateQueries({ queryKey: [bookmarksKey] }),
  })
}

export function useDeleteBookmark() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.del<void>(`/bookmarks/${id}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: [bookmarksKey] }),
  })
}

export function useMarkRead() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, isRead }: { id: string; isRead: boolean }) =>
      api.patch<Bookmark>(`/bookmarks/${id}/read`, { isRead }),
    onSuccess: () => qc.invalidateQueries({ queryKey: [bookmarksKey] }),
  })
}

/**
 * Sets the read state of every bookmark matching the current filters, across all pages.
 * Resolves to the number of bookmarks whose state actually changed, which can be lower than
 * the number matched (rows already in the target state are left alone).
 */
export function useMarkAllRead() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ query, isRead }: { query: BookmarkQuery; isRead: boolean }) =>
      // Paging and sorting are stripped: the action deliberately spans every match. The filters
      // go in the query string exactly as the GET sends them, so the set marked is the set shown.
      api.post<{ updated: number }>(
        '/bookmarks/mark-read',
        { isRead },
        narrowingFilters(query) as Record<string, unknown>,
      ),
    // The dashboard's counts come from the same key, so one invalidation refreshes both.
    onSuccess: () => qc.invalidateQueries({ queryKey: [bookmarksKey] }),
  })
}

import {
  keepPreviousData,
  useMutation,
  useQuery,
  useQueryClient,
} from '@tanstack/react-query'
import { api } from '@/lib/api-client'
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

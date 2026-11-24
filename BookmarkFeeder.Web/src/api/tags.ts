import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/lib/api-client'
import type { CreateTagRequest, Tag, UpdateTagRequest } from '@/types/models'
import { bookmarksKey, tagsKey } from './query-keys'

export { tagsKey }

/** A renamed or deleted tag changes the tag names shown on bookmarks, and the tag filter. */
function invalidateTagsAndBookmarks(qc: ReturnType<typeof useQueryClient>) {
  return Promise.all([
    qc.invalidateQueries({ queryKey: [tagsKey] }),
    qc.invalidateQueries({ queryKey: [bookmarksKey] }),
  ])
}

export function useTags() {
  return useQuery({
    queryKey: [tagsKey, 'list'],
    queryFn: () => api.get<Tag[]>('/tags'),
  })
}

export function useCreateTag() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: CreateTagRequest) => api.post<Tag>('/tags', body),
    onSuccess: () => qc.invalidateQueries({ queryKey: [tagsKey] }),
  })
}

export function useUpdateTag() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: UpdateTagRequest }) =>
      api.put<Tag>(`/tags/${id}`, body),
    onSuccess: () => invalidateTagsAndBookmarks(qc),
  })
}

export function useDeleteTag() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.del<void>(`/tags/${id}`),
    onSuccess: () => invalidateTagsAndBookmarks(qc),
  })
}

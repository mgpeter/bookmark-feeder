import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/lib/api-client'
import type { CreateTagRequest, Tag, UpdateTagRequest } from '@/types/models'

export const tagsKey = 'tags'

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
    onSuccess: () => qc.invalidateQueries({ queryKey: [tagsKey] }),
  })
}

export function useDeleteTag() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.del<void>(`/tags/${id}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: [tagsKey] }),
  })
}

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/lib/api-client'
import type { SavedSearch } from '@/types/models'

export const savedSearchesKey = 'saved-searches'

export function useSavedSearches() {
  return useQuery({
    queryKey: [savedSearchesKey],
    queryFn: () => api.get<SavedSearch[]>('/searches'),
  })
}

export function useCreateSavedSearch() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: { name: string; query: string }) =>
      api.post<SavedSearch>('/searches', body),
    onSuccess: () => qc.invalidateQueries({ queryKey: [savedSearchesKey] }),
  })
}

export function useDeleteSavedSearch() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.del<void>(`/searches/${id}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: [savedSearchesKey] }),
  })
}

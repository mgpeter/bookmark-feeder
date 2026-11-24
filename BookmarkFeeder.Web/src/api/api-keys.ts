import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/lib/api-client'
import type { ApiKey, CreatedApiKey } from '@/types/models'

export const apiKeysKey = 'api-keys'

export function useApiKeys() {
  return useQuery({
    queryKey: [apiKeysKey],
    queryFn: () => api.get<ApiKey[]>('/keys'),
  })
}

/**
 * Mints a key. The response contains the plaintext exactly once; the caller copies it into its own
 * state and must then call reset() so the mutation lets go of it too.
 *
 * Nothing writes the result into the query cache (onSuccess only invalidates), but the mutation
 * itself keeps its data in the MutationCache, and would for the default five-minute gcTime after
 * the card unmounts. gcTime: 0 drops it as soon as no component observes it.
 */
export function useCreateApiKey() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: { name: string; expiresInDays: number | null }) =>
      api.post<CreatedApiKey>('/keys', body),
    onSuccess: () => qc.invalidateQueries({ queryKey: [apiKeysKey] }),
    gcTime: 0,
  })
}

export function useRevokeApiKey() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.del<void>(`/keys/${id}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: [apiKeysKey] }),
  })
}

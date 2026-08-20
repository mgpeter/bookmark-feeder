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
 * Mints a key. The response contains the plaintext exactly once; the caller shows it and forgets
 * it. Note there is no onSuccess cache write of the result - only an invalidate - so the secret
 * never lands in the query cache.
 */
export function useCreateApiKey() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: { name: string; expiresInDays: number | null }) =>
      api.post<CreatedApiKey>('/keys', body),
    onSuccess: () => qc.invalidateQueries({ queryKey: [apiKeysKey] }),
  })
}

export function useRevokeApiKey() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.del<void>(`/keys/${id}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: [apiKeysKey] }),
  })
}

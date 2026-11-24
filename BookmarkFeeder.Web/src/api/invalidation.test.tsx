import { beforeEach, describe, expect, it, vi } from 'vitest'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'

import { setTokenGetter } from '@/lib/api-client'
import { useCreateBookmark, useDeleteBookmark, useUpdateBookmark } from './bookmarks'
import { useDeleteCategory, useUpdateCategory } from './categories'
import { useDeleteTag, useUpdateTag } from './tags'

setTokenGetter(async () => 'test-token')

/** Runs a mutation hook against a 200 and returns the root keys it invalidated. */
async function invalidatedBy<TVars>(
  useHook: () => { mutate: (vars: TVars) => void; isSuccess: boolean },
  vars: TVars,
): Promise<string[]> {
  vi.stubGlobal(
    'fetch',
    vi.fn().mockImplementation(
      async () =>
        new Response('{}', { status: 200, headers: { 'Content-Type': 'application/json' } }),
    ),
  )
  const client = new QueryClient({ defaultOptions: { mutations: { retry: false } } })
  const spy = vi.spyOn(client, 'invalidateQueries')
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  )

  const { result } = renderHook(useHook, { wrapper })
  result.current.mutate(vars)
  await waitFor(() => expect(result.current.isSuccess).toBe(true))

  return spy.mock.calls.map(([filters]) => String(filters?.queryKey?.[0])).sort()
}

describe('mutation invalidation', () => {
  beforeEach(() => vi.restoreAllMocks())

  it('refreshes tags and categories when a bookmark is created, edited or deleted', async () => {
    const all = ['bookmarks', 'categories', 'tags']
    expect(await invalidatedBy(useCreateBookmark, { url: 'https://a.test', title: 'A' })).toEqual(all)
    expect(await invalidatedBy(useUpdateBookmark, { id: '1', body: { tags: ['new'] } })).toEqual(all)
    expect(await invalidatedBy(useDeleteBookmark, '1')).toEqual(all)
  })

  it('refreshes bookmarks when a tag is renamed or deleted', async () => {
    expect(await invalidatedBy(useUpdateTag, { id: '1', body: { name: 'b' } })).toEqual([
      'bookmarks',
      'tags',
    ])
    expect(await invalidatedBy(useDeleteTag, '1')).toEqual(['bookmarks', 'tags'])
  })

  it('refreshes bookmarks when a category is renamed, deleted or reassigned', async () => {
    const both = ['bookmarks', 'categories']
    expect(await invalidatedBy(useUpdateCategory, { id: '1', body: { name: 'b' } })).toEqual(both)
    expect(await invalidatedBy(useDeleteCategory, { id: '1', reassignTo: '2' })).toEqual(both)
  })
})

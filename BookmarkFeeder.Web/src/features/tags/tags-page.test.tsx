import { beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'

import { setTokenGetter } from '@/lib/api-client'
import { TagsPage } from './tags-page'

setTokenGetter(async () => 'test-token')

describe('TagsPage', () => {
  beforeEach(() => vi.restoreAllMocks())

  it('sends one create while the first is still in flight, however often Enter is pressed', async () => {
    const user = userEvent.setup()
    const fetchMock = vi.fn().mockImplementation(async (_url: string, init: RequestInit) =>
      init.method === 'POST'
        ? new Promise<Response>(() => {}) // never settles: the request stays in flight
        : new Response('[]', { status: 200, headers: { 'Content-Type': 'application/json' } }),
    )
    vi.stubGlobal('fetch', fetchMock)
    const client = new QueryClient({
      defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    })
    render(
      <QueryClientProvider client={client}>
        <TagsPage />
      </QueryClientProvider>,
    )

    await user.click(screen.getByRole('button', { name: /new tag/i }))
    await user.type(screen.getByLabelText(/name/i), 'dotnet{Enter}{Enter}{Enter}')

    const posts = fetchMock.mock.calls.filter(([, init]) => (init as RequestInit).method === 'POST')
    expect(posts).toHaveLength(1)
    expect(screen.getByRole('button', { name: /^create$/i })).toBeDisabled()
  })
})

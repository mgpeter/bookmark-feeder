import { beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { toast } from 'sonner'

import { setTokenGetter } from '@/lib/api-client'
import { ApiKeysCard } from './api-keys-card'

vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }))

setTokenGetter(async () => 'test-token')

const keys = [
  { id: 'k1', name: 'Work laptop', prefix: 'bmf_7Kd9', createdAt: '2026-07-14T10:00:00Z' },
]

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

function setup(fetchMock: ReturnType<typeof vi.fn>) {
  vi.stubGlobal('fetch', fetchMock)
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  render(
    <QueryClientProvider client={client}>
      <ApiKeysCard />
    </QueryClientProvider>,
  )
  return client
}

const deletes = (fetchMock: ReturnType<typeof vi.fn>) =>
  fetchMock.mock.calls.filter(([, init]) => (init as RequestInit).method === 'DELETE')

describe('ApiKeysCard revoke', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    vi.mocked(toast.error).mockClear()
    vi.mocked(toast.success).mockClear()
  })

  it('asks for confirmation before revoking, and cancelling sends nothing', async () => {
    const user = userEvent.setup()
    const fetchMock = vi.fn().mockImplementation(async () => json(keys))
    setup(fetchMock)

    await user.click(await screen.findByRole('button', { name: /^revoke$/i }))
    expect(screen.getByRole('alertdialog')).toHaveTextContent(/work laptop/i)

    await user.click(screen.getByRole('button', { name: /cancel/i }))
    expect(deletes(fetchMock)).toHaveLength(0)
  })

  it('shows an error toast when the revoke fails', async () => {
    const user = userEvent.setup()
    const fetchMock = vi
      .fn()
      .mockImplementation(async (_url: string, init: RequestInit) =>
        init.method === 'DELETE' ? json({ message: 'Server unavailable' }, 503) : json(keys),
      )
    setup(fetchMock)

    await user.click(await screen.findByRole('button', { name: /^revoke$/i }))
    const dialog = screen.getByRole('alertdialog')
    await user.click(within(dialog).getByRole('button', { name: /^revoke$/i }))

    await waitFor(() => expect(toast.error).toHaveBeenCalled())
    expect(vi.mocked(toast.error).mock.calls[0][0]).toMatch(/could not revoke "Work laptop"/i)
    expect(deletes(fetchMock)).toHaveLength(1)
    expect(toast.success).not.toHaveBeenCalled()
  })
})

describe('ApiKeysCard mint', () => {
  beforeEach(() => vi.restoreAllMocks())

  it('shows the new key but leaves no copy of it in the mutation cache', async () => {
    const user = userEvent.setup()
    const plaintext = 'bmf_7Kd9_super-secret-plaintext'
    const client = setup(
      vi.fn().mockImplementation(async (_url: string, init: RequestInit) =>
        init.method === 'POST'
          ? json({ ...keys[0], id: 'k2', name: 'Home', key: plaintext })
          : json(keys),
      ),
    )

    await user.type(await screen.findByLabelText(/^name$/i), 'Home')
    await user.click(screen.getByRole('button', { name: /create key/i }))

    expect(await screen.findByDisplayValue(plaintext)).toBeInTheDocument()
    await waitFor(() => {
      const cached = client.getMutationCache().getAll().map((m) => m.state.data)
      expect(JSON.stringify(cached)).not.toContain(plaintext)
    })
  })
})

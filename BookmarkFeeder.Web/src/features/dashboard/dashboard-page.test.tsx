import { beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'

import { setTokenGetter } from '@/lib/api-client'
import { DashboardPage } from './dashboard-page'

setTokenGetter(async () => 'test-token')

describe('DashboardPage', () => {
  beforeEach(() => vi.restoreAllMocks())

  it('shows a failed load as an error with a retry, not as zero or empty', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockImplementation(async () => new Response('', { status: 500 })),
    )
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    render(
      <QueryClientProvider client={client}>
        <MemoryRouter>
          <DashboardPage />
        </MemoryRouter>
      </QueryClientProvider>,
    )

    expect(await screen.findByText(/could not load recent bookmarks/i)).toBeInTheDocument()
    expect(screen.getByText(/could not load tags/i)).toBeInTheDocument()
    expect(screen.getAllByRole('button', { name: /retry/i }).length).toBeGreaterThan(0)
    expect(screen.queryByText(/no bookmarks yet/i)).not.toBeInTheDocument()
    expect(screen.queryByText('0')).not.toBeInTheDocument()
  })
})

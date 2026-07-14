import { beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { installChromeStub, sampleTree } from '@/test/chrome-stub'
import { App } from './App'

const configured = {
  serverUrl: 'http://localhost:5180/api',
  apiKey: 'dev-key',
  selectedFolders: [{ id: '2', title: 'Dev', depth: 2 }],
}

function mockFetch(body: unknown, status = 200) {
  const fetchMock = vi.fn().mockResolvedValue(
    new Response(JSON.stringify(body), {
      status,
      headers: { 'Content-Type': 'application/json' },
    }),
  )
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

const summary = { total: 2, created: 2, skipped: 0, errors: 0 }

describe('App', () => {
  beforeEach(() => {
    vi.unstubAllGlobals()
    vi.restoreAllMocks()
  })

  it('persists the selection when a folder is toggled', async () => {
    const user = userEvent.setup()
    const { stub } = installChromeStub({ tree: sampleTree(), storage: configured })
    render(<App />)

    await user.click(await screen.findByRole('tab', { name: /folders/i }))
    // 'Dev' starts selected; toggling 'React' adds it to the stored selection.
    await user.click(await screen.findByRole('checkbox', { name: 'React' }))

    await waitFor(() =>
      expect(stub.storage.sync.set).toHaveBeenCalledWith({
        selectedFolders: [
          { id: '2', title: 'Dev', depth: 2 },
          { id: '3', title: 'React', depth: 3 },
        ],
      }),
    )
  })

  it('unchecks a selected folder and removes it from storage', async () => {
    const user = userEvent.setup()
    const { stub } = installChromeStub({ tree: sampleTree(), storage: configured })
    render(<App />)

    await user.click(await screen.findByRole('tab', { name: /folders/i }))
    await user.click(await screen.findByRole('checkbox', { name: 'Dev' }))

    await waitFor(() =>
      expect(stub.storage.sync.set).toHaveBeenCalledWith({ selectedFolders: [] }),
    )
  })

  it('syncs the selected folders and reports the summary', async () => {
    const user = userEvent.setup()
    installChromeStub({ tree: sampleTree(), storage: configured })
    const fetchMock = mockFetch({ summary })
    render(<App />)

    await user.click(await screen.findByRole('button', { name: /sync now/i }))

    await waitFor(() => expect(fetchMock).toHaveBeenCalled())
    const [url] = fetchMock.mock.calls[0] as [string]
    expect(url).toBe('http://localhost:5180/api/bookmarks/batch')
    expect(await screen.findByText(/2 created/i)).toBeInTheDocument()
  })

  it('surfaces a sync failure without claiming success', async () => {
    const user = userEvent.setup()
    installChromeStub({ tree: sampleTree(), storage: configured })
    mockFetch({}, 401)
    render(<App />)

    await user.click(await screen.findByRole('button', { name: /sync now/i }))

    expect(await screen.findByText(/check your api key/i)).toBeInTheDocument()
  })

  it('opens the dashboard in a new tab at the origin derived from the server URL', async () => {
    const user = userEvent.setup()
    const { stub } = installChromeStub({ tree: sampleTree(), storage: configured })
    render(<App />)

    await user.click(await screen.findByRole('button', { name: /dashboard/i }))

    expect(stub.tabs.create).toHaveBeenCalledWith({ url: 'http://localhost:5180' })
  })

  it('starts on Settings when the server URL is not configured', async () => {
    installChromeStub({ tree: sampleTree() })
    render(<App />)

    expect(await screen.findByLabelText(/server url/i)).toBeInTheDocument()
  })

  // The placeholder shows the URL you need, so an empty field looks filled in.
  // Save must explain itself rather than sit there disabled.
  it('explains why it cannot save when the server URL is left empty', async () => {
    const user = userEvent.setup()
    const { stub } = installChromeStub({ tree: sampleTree() })
    render(<App />)

    const save = await screen.findByRole('button', { name: /^save$/i })
    expect(save).toBeEnabled()
    await user.click(save)

    expect(await screen.findByText(/enter your server url/i)).toBeInTheDocument()
    expect(stub.storage.sync.set).not.toHaveBeenCalled()
  })

  it('saves settings and rejects a malformed server URL', async () => {
    const user = userEvent.setup()
    const { stub } = installChromeStub({ tree: sampleTree() })
    render(<App />)

    const urlInput = await screen.findByLabelText(/server url/i)
    await user.type(urlInput, 'not-a-url')
    await user.click(screen.getByRole('button', { name: /^save$/i }))

    expect(await screen.findByText(/valid url/i)).toBeInTheDocument()
    expect(stub.storage.sync.set).not.toHaveBeenCalled()

    await user.clear(urlInput)
    await user.type(urlInput, 'http://localhost:5180/api')
    await user.type(screen.getByLabelText(/api key/i), 'k')
    await user.click(screen.getByRole('button', { name: /^save$/i }))

    await waitFor(() =>
      expect(stub.storage.sync.set).toHaveBeenCalledWith({
        serverUrl: 'http://localhost:5180/api',
        apiKey: 'k',
      }),
    )
  })

  it('keeps the API key masked', async () => {
    const user = userEvent.setup()
    installChromeStub({ tree: sampleTree(), storage: configured })
    render(<App />)

    await user.click(await screen.findByRole('tab', { name: /settings/i }))

    expect(await screen.findByLabelText(/api key/i)).toHaveAttribute('type', 'password')
  })
})

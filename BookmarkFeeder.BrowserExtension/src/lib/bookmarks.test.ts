import { beforeEach, describe, expect, it, vi } from 'vitest'
import { installChromeStub, sampleTree } from '@/test/chrome-stub'
import { collectBookmarks, collectFromFolders, listFolders } from './bookmarks'

describe('listFolders', () => {
  beforeEach(() => vi.unstubAllGlobals())

  it('lists only folders, skipping the root and bookmark leaves, with depth', async () => {
    installChromeStub({ tree: sampleTree() })

    const folders = await listFolders()

    expect(folders).toEqual([
      { id: '1', title: 'Bookmarks Bar', depth: 1 },
      { id: '2', title: 'Dev', depth: 2 },
      { id: '3', title: 'React', depth: 3 },
    ])
  })
})

describe('collectBookmarks', () => {
  it('walks a subtree building the sourceFolder path from the selected folder', () => {
    const [root] = sampleTree()
    const dev = root.children![0].children![0]

    const items = collectBookmarks(dev, 'Dev')

    expect(items).toEqual([
      {
        url: 'https://vite.dev',
        title: 'Vite',
        description: null,
        sourceFolder: 'Dev',
        dateAdded: 1700000000000,
      },
      {
        url: 'https://react.dev',
        title: 'React docs',
        description: null,
        sourceFolder: 'Dev/React',
        dateAdded: 1700000001000,
      },
    ])
  })

  it('falls back to the url as title and to null dateAdded', () => {
    const node = { id: '9', title: '', url: 'https://no-title.example' }

    expect(collectBookmarks(node as chrome.bookmarks.BookmarkTreeNode, 'F')).toEqual([
      {
        url: 'https://no-title.example',
        title: 'https://no-title.example',
        description: null,
        sourceFolder: 'F',
        dateAdded: null,
      },
    ])
  })

  // The API validates the whole batch; one oversized field would reject every bookmark.
  it('truncates title and sourceFolder to the API field limits', () => {
    const node = {
      id: '9',
      title: 'T'.repeat(600),
      url: 'https://long.example',
    } as chrome.bookmarks.BookmarkTreeNode

    const [item] = collectBookmarks(node, 'P'.repeat(1200))

    expect(item.title).toHaveLength(500)
    expect(item.sourceFolder).toHaveLength(1024)
  })

  it('drops bookmarks whose url exceeds the API limit rather than failing the batch', () => {
    const node = {
      id: '9',
      title: 'Bookmarklet',
      url: `javascript:${'x'.repeat(2100)}`,
    } as chrome.bookmarks.BookmarkTreeNode

    expect(collectBookmarks(node, 'F')).toEqual([])
  })
})

describe('collectFromFolders', () => {
  beforeEach(() => vi.unstubAllGlobals())

  it('collects across selected folders', async () => {
    installChromeStub({ tree: sampleTree() })

    const items = await collectFromFolders([{ id: '2', title: 'Dev', depth: 2 }])

    expect(items.map((i) => i.url)).toEqual(['https://vite.dev', 'https://react.dev'])
  })

  it('skips folders that no longer exist instead of failing the sync', async () => {
    installChromeStub({ tree: sampleTree() })

    const items = await collectFromFolders([
      { id: 'gone', title: 'Deleted', depth: 1 },
      { id: '3', title: 'React', depth: 3 },
    ])

    expect(items.map((i) => i.url)).toEqual(['https://react.dev'])
  })
})

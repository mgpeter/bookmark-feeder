import { vi } from 'vitest'

type Node = chrome.bookmarks.BookmarkTreeNode

interface ChromeStubOptions {
  /** Initial contents of `chrome.storage.sync`. */
  storage?: Record<string, unknown>
  /** The tree returned by `chrome.bookmarks.getTree` (root node first). */
  tree?: Node[]
}

function findNode(id: string, nodes: Node[]): Node | undefined {
  for (const node of nodes) {
    if (node.id === id) return node
    const hit = node.children && findNode(id, node.children)
    if (hit) return hit
  }
  return undefined
}

/**
 * Installs an in-memory `chrome` global covering the APIs the sync modules use.
 * Returns the stub (for call assertions) and the live storage object.
 */
export function installChromeStub({ storage = {}, tree = [] }: ChromeStubOptions = {}) {
  const store: Record<string, unknown> = structuredClone(storage)

  const stub = {
    storage: {
      sync: {
        // Mirrors chrome's contract: only keys that exist are returned.
        get: vi.fn(async (keys: string[]) =>
          Object.fromEntries(
            keys.filter((key) => key in store).map((key) => [key, store[key]]),
          ),
        ),
        set: vi.fn(async (items: Record<string, unknown>) => {
          Object.assign(store, items)
        }),
      },
    },
    bookmarks: {
      getTree: vi.fn(async () => tree),
      getSubTree: vi.fn(async (id: string) => {
        const node = findNode(id, tree)
        // Chrome rejects rather than returning empty for an unknown id.
        if (!node) throw new Error("Can't find bookmark for id.")
        return [node]
      }),
    },
    tabs: {
      create: vi.fn(async (props: { url: string }) => ({ id: 1, ...props })),
    },
  }

  vi.stubGlobal('chrome', stub)
  return { stub, store }
}

/** A small but realistic bookmark tree: root → Bookmarks Bar → Dev → React. */
export function sampleTree(): Node[] {
  return [
    {
      id: '0',
      title: '',
      children: [
        {
          id: '1',
          title: 'Bookmarks Bar',
          children: [
            {
              id: '2',
              title: 'Dev',
              children: [
                { id: '4', title: 'Vite', url: 'https://vite.dev', dateAdded: 1700000000000 },
                {
                  id: '3',
                  title: 'React',
                  children: [
                    { id: '5', title: 'React docs', url: 'https://react.dev', dateAdded: 1700000001000 },
                  ],
                },
              ],
            },
            { id: '6', title: 'Loose link', url: 'https://example.com' },
          ],
        },
      ],
    },
  ] as Node[]
}

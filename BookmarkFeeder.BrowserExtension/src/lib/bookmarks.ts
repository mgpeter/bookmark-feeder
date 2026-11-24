import { FIELD_LIMITS, type BatchBookmarkItem, type BookmarkFolder } from './types'

type Node = chrome.bookmarks.BookmarkTreeNode

/** Bundle id of the Safari wrapper app; Safari routes native messages to its extension handler. */
const SAFARI_APP_ID = 'com.usualexpat.BookmarkFeeder'

/** Thrown when bookmarks exist but cannot be read; `code` comes from the native handler. */
export class BookmarksAccessError extends Error {
  code: string

  constructor(code: string, message: string) {
    super(message)
    this.name = 'BookmarksAccessError'
    this.code = code
  }
}

type NativeReply = { tree: Node } | { error: string; message?: string }

/**
 * Safari has no `bookmarks` API for extensions. Its build instead carries `nativeMessaging`, and
 * the wrapper app's handler (safari/…/SafariBookmarks.swift) reads Safari's Bookmarks.plist and
 * returns it shaped like a `chrome.bookmarks` tree. Chrome lacks `sendNativeMessage` without that
 * permission, so this only kicks in for Safari.
 */
function usesNativeBookmarks(): boolean {
  return (
    typeof chrome !== 'undefined' &&
    !chrome.bookmarks &&
    typeof chrome.runtime?.sendNativeMessage === 'function'
  )
}

async function nativeTree(): Promise<Node[]> {
  let reply: NativeReply | undefined
  try {
    reply = (await chrome.runtime.sendNativeMessage(SAFARI_APP_ID, { type: 'getTree' })) as
      | NativeReply
      | undefined
  } catch (error) {
    throw new BookmarksAccessError('native-unavailable', String(error))
  }
  if (!reply) throw new BookmarksAccessError('native-unavailable', 'No reply from the BookmarkFeeder app.')
  if ('error' in reply) throw new BookmarksAccessError(reply.error, reply.message ?? reply.error)
  return [reply.tree]
}

function findNode(id: string, nodes: Node[]): Node | undefined {
  for (const node of nodes) {
    if (node.id === id) return node
    const hit = node.children && findNode(id, node.children)
    if (hit) return hit
  }
  return undefined
}

/** `chrome.bookmarks.getTree`, or Safari's native equivalent. */
function getTree(): Promise<Node[]> {
  return usesNativeBookmarks() ? nativeTree() : chrome.bookmarks.getTree()
}

/**
 * Whether bookmarks can be read at all: Chrome and Edge through `chrome.bookmarks`, Safari through
 * the native handler.
 */
export function isBookmarksApiAvailable(): boolean {
  return (
    typeof chrome !== 'undefined' &&
    (typeof chrome.bookmarks?.getTree === 'function' || usesNativeBookmarks())
  )
}

/** Lists every folder in the bookmark tree, skipping the unnamed root. */
export async function listFolders(): Promise<BookmarkFolder[]> {
  const tree = await getTree()
  return tree.flatMap((root) => walkFolders(root, 0))
}

function walkFolders(node: Node, depth: number): BookmarkFolder[] {
  if (!node.children) return []

  const self = depth > 0 ? [{ id: node.id, title: node.title, depth }] : []
  const nested = node.children.flatMap((child) => walkFolders(child, depth + 1))
  return [...self, ...nested]
}

/**
 * Walks a folder subtree into sync items, tracking the folder path as `sourceFolder`.
 * `path` is the path of `node` itself, so paths are relative to the selected folder.
 */
export function collectBookmarks(node: Node, path: string): BatchBookmarkItem[] {
  if (node.url) {
    // Over-long URLs can't be truncated without breaking the link, so drop them:
    // keeping one would make the API reject the entire batch.
    if (node.url.length > FIELD_LIMITS.url) return []

    return [
      {
        url: node.url,
        title: truncate(node.title || node.url, FIELD_LIMITS.title),
        description: null,
        sourceFolder: truncate(path, FIELD_LIMITS.sourceFolder),
        dateAdded: node.dateAdded ?? null,
      },
    ]
  }

  return (node.children ?? []).flatMap((child) =>
    collectBookmarks(child, child.url ? path : `${path}/${child.title}`),
  )
}

/** Collects the bookmarks of every selected folder into one payload. */
export async function collectFromFolders(
  folders: BookmarkFolder[],
): Promise<BatchBookmarkItem[]> {
  const items: BatchBookmarkItem[] = []
  // Safari's native handler only returns the whole tree, so read it once for every folder.
  const safariTree = usesNativeBookmarks() ? await nativeTree() : null

  for (const folder of folders) {
    let node: Node | undefined
    if (safariTree) {
      node = findNode(folder.id, safariTree)
    } else {
      try {
        node = (await chrome.bookmarks.getSubTree(folder.id))[0]
      } catch {
        node = undefined
      }
    }
    // A folder deleted in the browser but still in our stored selection is skipped rather than
    // failing the whole sync.
    if (node) items.push(...collectBookmarks(node, folder.title))
  }

  return items
}

function truncate(value: string, max: number): string {
  return value.length > max ? value.slice(0, max) : value
}

import { FIELD_LIMITS, type BatchBookmarkItem, type BookmarkFolder } from './types'

type Node = chrome.bookmarks.BookmarkTreeNode

/** Lists every folder in the bookmark tree, skipping the unnamed root. */
export async function listFolders(): Promise<BookmarkFolder[]> {
  const tree = await chrome.bookmarks.getTree()
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

  for (const folder of folders) {
    let subTree: Node[]
    try {
      subTree = await chrome.bookmarks.getSubTree(folder.id)
    } catch {
      // The folder was deleted in the browser but is still in our stored selection;
      // skip it rather than failing the whole sync.
      continue
    }
    if (subTree[0]) items.push(...collectBookmarks(subTree[0], folder.title))
  }

  return items
}

function truncate(value: string, max: number): string {
  return value.length > max ? value.slice(0, max) : value
}

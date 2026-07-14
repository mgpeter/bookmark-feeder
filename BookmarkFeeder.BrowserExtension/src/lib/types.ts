/** A bookmark folder that can be selected for syncing. */
export interface BookmarkFolder {
  id: string
  title: string
  /** Depth in the bookmark tree; the root's children are depth 1. */
  depth: number
}

/** One item of the sync payload. Mirrors the API's `BatchBookmarkItem`. */
export interface BatchBookmarkItem {
  url: string
  title: string
  description: string | null
  sourceFolder: string
  /** Chrome epoch milliseconds. */
  dateAdded: number | null
}

/** Mirrors the API's `BatchSummary`. */
export interface BatchSummary {
  total: number
  created: number
  skipped: number
  errors: number
}

/** Mirrors the API's `BatchResultDto` (only the parts the extension uses). */
export interface BatchResult {
  summary: BatchSummary
}

/** Everything the extension persists in `chrome.storage.sync`. */
export interface Settings {
  /** Base API URL, e.g. http://localhost:5180/api */
  serverUrl: string
  apiKey: string
  selectedFolders: BookmarkFolder[]
  /** ISO timestamp of the last successful sync, or null if never synced. */
  lastSync: string | null
}

/**
 * Field limits enforced by the API's BatchCreateRequestValidator. It validates the whole
 * request, so a single oversized field would reject every bookmark in the batch.
 */
export const FIELD_LIMITS = {
  url: 2048,
  title: 500,
  sourceFolder: 1024,
} as const

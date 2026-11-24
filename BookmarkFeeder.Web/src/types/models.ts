// Wire shapes mirror the .NET API DTOs (System.Text.Json camelCase).

export interface TagRef {
  id: string
  name: string
  normalizedName: string
  color?: string | null
}

export interface CategoryRef {
  id: string
  name: string
}

export interface Bookmark {
  id: string
  url: string
  title: string
  description?: string | null
  faviconUrl?: string | null
  sourceFolder?: string | null
  isRead: boolean
  dateAdded: string
  dateModified: string
  tags: TagRef[]
  /** 0-or-1 element (one category per bookmark). */
  categories: CategoryRef[]
}

export interface Pagination {
  page: number
  pageSize: number
  totalItems: number
  totalPages: number
}

export interface PagedResult<T> {
  data: T[]
  pagination: Pagination
}

export interface FacetItem {
  id: string
  name: string
  count: number
}

export interface BookmarkFacets {
  tags: FacetItem[]
  categories: FacetItem[]
}

/** The bookmark list: a page plus, when something narrows it, the facet counts. */
export interface BookmarkListResult extends PagedResult<Bookmark> {
  /** Absent when no search or filter is active — there would be nothing to refine. */
  facets?: BookmarkFacets | null
}

export interface SavedSearch {
  id: string
  name: string
  /** The serialized filter string, e.g. "q=graphql&tags=dotnet". */
  query: string
  dateCreated: string
}

export interface Tag {
  id: string
  name: string
  normalizedName: string
  color?: string | null
  dateCreated: string
  bookmarkCount: number
}

export interface Category {
  id: string
  name: string
  description?: string | null
  parentCategoryId?: string | null
  level: number
  bookmarkCount: number
  dateCreated: string
  children: Category[]
}

export type SortBy = 'relevance' | 'dateAdded' | 'dateModified' | 'title' | 'url'
export type SortOrder = 'asc' | 'desc'

export interface BookmarkQuery {
  page?: number
  pageSize?: number
  search?: string
  /** comma-separated tag names */
  tags?: string
  /** comma-separated category ids */
  categories?: string
  sourceFolder?: string
  isRead?: boolean
  dateFrom?: string
  dateTo?: string
  sortBy?: SortBy
  sortOrder?: SortOrder
}

export interface CreateBookmarkRequest {
  url: string
  title: string
  description?: string | null
  faviconUrl?: string | null
  sourceFolder?: string | null
  categoryId?: string | null
  tags?: string[]
}

/** Partial update: omitted or null fields are left unchanged. */
export interface UpdateBookmarkRequest {
  title?: string
  /** An empty string clears the description. */
  description?: string | null
  faviconUrl?: string | null
  isRead?: boolean
  categoryId?: string | null
  /** True removes the category (categoryId must then be omitted). */
  clearCategory?: boolean
  tags?: string[]
}

export interface CreateTagRequest {
  name: string
  color?: string | null
}

export interface UpdateTagRequest {
  name: string
  color?: string | null
}

export interface CreateCategoryRequest {
  name: string
  description?: string | null
  parentCategoryId?: string | null
}

export interface UpdateCategoryRequest {
  name: string
  description?: string | null
  parentCategoryId?: string | null
}

/** API key metadata. Carries no secret, so it is safe to hold in the query cache. */
export interface ApiKey {
  id: string
  name: string
  /** Display identity, e.g. "bmf_7Kd9". Not enough to authenticate with. */
  prefix: string
  createdAt: string
  lastUsedAt?: string | null
  expiresAt?: string | null
  revokedAt?: string | null
}

/**
 * The create response, and the only shape that ever carries the plaintext key. Deliberately kept
 * out of the query cache - it is rendered once and then dropped.
 */
export interface CreatedApiKey extends Omit<ApiKey, 'lastUsedAt' | 'revokedAt'> {
  key: string
}

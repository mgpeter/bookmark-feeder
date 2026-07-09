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

export type SortBy = 'dateAdded' | 'dateModified' | 'title' | 'url'
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

export interface UpdateBookmarkRequest {
  title?: string
  description?: string | null
  faviconUrl?: string | null
  isRead?: boolean
  categoryId?: string | null
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

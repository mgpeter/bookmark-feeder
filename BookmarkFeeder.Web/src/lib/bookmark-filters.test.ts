import { describe, expect, it } from 'vitest'
import { hasNarrowingFilters, narrowingFilters } from './bookmark-filters'

describe('narrowingFilters', () => {
  it('drops paging and sorting, which do not narrow the set', () => {
    const filters = narrowingFilters({
      page: 3,
      pageSize: 20,
      sortBy: 'title',
      sortOrder: 'desc',
      search: 'graphql',
    })

    expect(filters).toEqual({ search: 'graphql' })
  })

  it('keeps every narrowing field', () => {
    const query = {
      search: 'a',
      tags: 'dotnet',
      categories: 'id-1',
      sourceFolder: 'Research',
      isRead: true,
      dateFrom: '2026-01-01',
      dateTo: '2026-02-01',
    }

    expect(narrowingFilters(query)).toEqual(query)
  })

  it('keeps isRead=false — an unread filter is a real filter, not an absent one', () => {
    expect(narrowingFilters({ isRead: false })).toEqual({ isRead: false })
  })

  it('drops empty strings and undefined', () => {
    expect(narrowingFilters({ search: '', tags: undefined, sourceFolder: 'x' })).toEqual({
      sourceFolder: 'x',
    })
  })
})

describe('hasNarrowingFilters', () => {
  it('is false when only paging and sorting are set', () => {
    expect(hasNarrowingFilters({ page: 2, pageSize: 20, sortBy: 'title' })).toBe(false)
  })

  it('is false for an empty query', () => {
    expect(hasNarrowingFilters({})).toBe(false)
  })

  it.each([
    ['search', { search: 'x' }],
    ['tags', { tags: 'dotnet' }],
    ['isRead=false', { isRead: false }],
    ['dateFrom', { dateFrom: '2026-01-01' }],
  ])('is true when %s is set', (_label, query) => {
    expect(hasNarrowingFilters(query)).toBe(true)
  })
})

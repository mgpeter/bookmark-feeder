import { describe, expect, it } from 'vitest'
import { renderHook } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import type { ReactNode } from 'react'
import { clampedPage, useBookmarkQuery } from './use-bookmark-query'

function at(url: string) {
  return {
    wrapper: ({ children }: { children: ReactNode }) => (
      <MemoryRouter initialEntries={[url]}>{children}</MemoryRouter>
    ),
  }
}

describe('useBookmarkQuery sorting', () => {
  it('defaults to relevance when a search term is present', () => {
    const { result } = renderHook(() => useBookmarkQuery(), at('/bookmarks?q=graphql'))

    // The UI always sends an explicit sortBy, so without this the API's own relevance
    // default could never fire and a search would silently sort by date.
    expect(result.current.query.sortBy).toBe('relevance')
  })

  it('defaults to dateAdded when there is no search term', () => {
    const { result } = renderHook(() => useBookmarkQuery(), at('/bookmarks'))

    expect(result.current.query.sortBy).toBe('dateAdded')
  })

  it('respects an explicit sort over the relevance default', () => {
    const { result } = renderHook(() => useBookmarkQuery(), at('/bookmarks?q=graphql&sort=title'))

    expect(result.current.query.sortBy).toBe('title')
  })

  it('reads the search term from the q param', () => {
    const { result } = renderHook(() => useBookmarkQuery(), at('/bookmarks?q=graphql'))

    expect(result.current.query.search).toBe('graphql')
  })
})

describe('clampedPage', () => {
  it('moves a page past the end to the last real page', () => {
    // The last bookmark on page 3 was deleted, leaving two pages.
    expect(clampedPage(3, 2)).toBe(2)
  })

  it('leaves a page within range alone', () => {
    expect(clampedPage(2, 2)).toBeNull()
    expect(clampedPage(1, 5)).toBeNull()
  })

  it('does nothing when the result is empty', () => {
    expect(clampedPage(4, 0)).toBeNull()
  })
})

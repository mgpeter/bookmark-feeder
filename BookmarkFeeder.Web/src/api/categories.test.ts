import { describe, expect, it } from 'vitest'
import { flattenCategories } from './categories'
import type { Category } from '@/types/models'

function cat(id: string, name: string, children: Category[] = []): Category {
  return {
    id,
    name,
    description: null,
    parentCategoryId: null,
    level: 0,
    bookmarkCount: 0,
    dateCreated: '2026-01-01T00:00:00Z',
    children,
  }
}

describe('flattenCategories', () => {
  it('flattens a tree depth-first with depth annotations', () => {
    const tree = [cat('1', 'A', [cat('2', 'B', [cat('3', 'C')])]), cat('4', 'D')]
    const flat = flattenCategories(tree)

    expect(flat.map((f) => f.category.id)).toEqual(['1', '2', '3', '4'])
    expect(flat.map((f) => f.depth)).toEqual([0, 1, 2, 0])
  })
})

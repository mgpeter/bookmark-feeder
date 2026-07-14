import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { FacetPanel } from './facet-panel'
import type { BookmarkFacets } from '@/types/models'

const facets: BookmarkFacets = {
  tags: [
    { id: 't1', name: 'dotnet', count: 12 },
    { id: 't2', name: 'react', count: 3 },
  ],
  categories: [{ id: 'c1', name: 'Technology', count: 8 }],
}

function setup(props: Partial<React.ComponentProps<typeof FacetPanel>> = {}) {
  const onToggleTag = vi.fn()
  const onSelectCategory = vi.fn()
  render(
    <FacetPanel
      facets={facets}
      selectedTags={[]}
      selectedCategory={undefined}
      onToggleTag={onToggleTag}
      onSelectCategory={onSelectCategory}
      {...props}
    />,
  )
  return { onToggleTag, onSelectCategory }
}

describe('FacetPanel', () => {
  it('shows each tag and category with its count', () => {
    setup()

    expect(screen.getByRole('button', { name: /dotnet.*12/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /react.*3/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /technology.*8/i })).toBeInTheDocument()
  })

  it('clicking a tag facet asks to add it as a filter', async () => {
    const user = userEvent.setup()
    const { onToggleTag } = setup()

    await user.click(screen.getByRole('button', { name: /dotnet/i }))

    expect(onToggleTag).toHaveBeenCalledWith('dotnet')
  })

  it('clicking a category facet asks to filter by its id', async () => {
    const user = userEvent.setup()
    const { onSelectCategory } = setup()

    await user.click(screen.getByRole('button', { name: /technology/i }))

    expect(onSelectCategory).toHaveBeenCalledWith('c1')
  })

  it('marks the active facets as pressed', () => {
    setup({ selectedTags: ['dotnet'], selectedCategory: 'c1' })

    expect(screen.getByRole('button', { name: /dotnet/i })).toHaveAttribute(
      'aria-pressed',
      'true',
    )
    expect(screen.getByRole('button', { name: /react/i })).toHaveAttribute(
      'aria-pressed',
      'false',
    )
    expect(screen.getByRole('button', { name: /technology/i })).toHaveAttribute(
      'aria-pressed',
      'true',
    )
  })

  it('renders nothing when there are no facets to show', () => {
    const { container } = render(
      <FacetPanel
        facets={{ tags: [], categories: [] }}
        selectedTags={[]}
        selectedCategory={undefined}
        onToggleTag={vi.fn()}
        onSelectCategory={vi.fn()}
      />,
    )

    expect(container).toBeEmptyDOMElement()
  })
})

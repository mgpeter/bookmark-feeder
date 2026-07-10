import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { TagChip } from './tag-chip'

describe('TagChip', () => {
  it('renders the tag name', () => {
    render(<TagChip name="React" />)
    expect(screen.getByText('React')).toBeInTheDocument()
  })

  it('applies the color when provided', () => {
    render(<TagChip name="Colored" color="#512BD4" />)
    const chip = screen.getByText('Colored')
    expect(chip).toHaveStyle({ color: '#512BD4' })
  })
})

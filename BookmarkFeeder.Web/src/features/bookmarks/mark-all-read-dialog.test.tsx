import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MarkAllReadDialog } from './mark-all-read-dialog'

function setup(props: Partial<React.ComponentProps<typeof MarkAllReadDialog>> = {}) {
  const onConfirm = vi.fn()
  const onOpenChange = vi.fn()
  render(
    <MarkAllReadDialog
      open
      count={137}
      isFiltered
      onConfirm={onConfirm}
      onOpenChange={onOpenChange}
      {...props}
    />,
  )
  return { onConfirm, onOpenChange }
}

describe('MarkAllReadDialog', () => {
  it('states the count and that it spans every page when filtered', () => {
    setup({ isFiltered: true, count: 137 })

    expect(screen.getByRole('alertdialog')).toHaveTextContent(/137 matching bookmarks/i)
    expect(screen.getByRole('alertdialog')).toHaveTextContent(/all pages/i)
  })

  it('warns that the whole collection is affected when nothing is filtered', () => {
    setup({ isFiltered: false, count: 4312 })

    const dialog = screen.getByRole('alertdialog')
    // The count and this wording are the only guard against sweeping everything.
    expect(dialog).toHaveTextContent(/entire collection/i)
    expect(dialog).not.toHaveTextContent(/matching/i)
  })

  it('formats large counts readably', () => {
    setup({ isFiltered: false, count: 4312 })

    expect(screen.getByRole('alertdialog')).toHaveTextContent('4,312')
  })

  it('confirms only when the confirm button is pressed', async () => {
    const user = userEvent.setup()
    const { onConfirm } = setup()

    await user.click(screen.getByRole('button', { name: /mark as read/i }))

    expect(onConfirm).toHaveBeenCalledOnce()
  })

  it('cancelling does not confirm', async () => {
    const user = userEvent.setup()
    const { onConfirm, onOpenChange } = setup()

    await user.click(screen.getByRole('button', { name: /cancel/i }))

    expect(onConfirm).not.toHaveBeenCalled()
    expect(onOpenChange).toHaveBeenCalledWith(false)
  })
})

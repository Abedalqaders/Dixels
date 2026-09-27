// @vitest-environment jsdom
import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Dialog } from './Dialog'

function renderDialog(onClose = vi.fn()) {
  render(
    <Dialog title="Book Room 1" onClose={onClose}>
      <input aria-label="Title" />
      <button type="button">Book</button>
    </Dialog>,
  )
  return onClose
}

describe('Dialog', () => {
  it('is announced as a modal dialog named by its title', () => {
    renderDialog()

    const dialog = screen.getByRole('dialog', { name: 'Book Room 1' })
    expect(dialog).toHaveAttribute('aria-modal', 'true')
  })

  it('focuses the first field when it opens', () => {
    renderDialog()

    expect(screen.getByLabelText('Title')).toHaveFocus()
  })

  it('keeps Tab inside the dialog', async () => {
    renderDialog()
    const user = userEvent.setup()

    await user.tab()
    expect(screen.getByRole('button', { name: 'Book' })).toHaveFocus()

    await user.tab()
    expect(screen.getByLabelText('Title')).toHaveFocus()

    await user.tab({ shift: true })
    expect(screen.getByRole('button', { name: 'Book' })).toHaveFocus()
  })

  it('closes on Escape', async () => {
    const onClose = renderDialog()

    await userEvent.setup().keyboard('{Escape}')

    expect(onClose).toHaveBeenCalledOnce()
  })
})

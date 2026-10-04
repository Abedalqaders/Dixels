// @vitest-environment jsdom
import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { setLanguage } from '@/i18n'
import { AddNodeModal } from './AddNodeModal'

function renderSpaceModal() {
  render(
    <AddNodeModal
      state={{ kind: 'space', parentName: 'Level 1', parentId: 'f1' }}
      token="t"
      spaceTypes={[]}
      onClose={vi.fn()}
      onCreated={vi.fn()}
      onError={vi.fn()}
    />,
  )
}

describe('AddNodeModal', () => {
  it('puts every problem under its own field, all at once', async () => {
    const user = userEvent.setup()
    renderSpaceModal()

    await user.click(screen.getByRole('button', { name: 'Add' }))

    const name = screen.getByLabelText(/Name/)
    expect(name).toHaveAttribute('aria-invalid', 'true')
    expect(name).toHaveAccessibleDescription('Name is required.')
    expect(screen.getByLabelText(/Capacity/)).toHaveAccessibleDescription('Capacity must be a positive number.')
    expect(screen.getByLabelText(/Type/)).toHaveAccessibleDescription('Choose a space type.')
  })

  it('focuses the first field that needs fixing', async () => {
    const user = userEvent.setup()
    renderSpaceModal()

    await user.type(screen.getByLabelText(/Name/), 'Desk 9')
    await user.click(screen.getByRole('button', { name: 'Add' }))

    expect(screen.getByLabelText(/Name/)).not.toHaveAttribute('aria-invalid')
    expect(screen.getByLabelText(/Capacity/)).toHaveFocus()
  })

  it('speaks Arabic, problems included', async () => {
    await setLanguage('ar')
    const user = userEvent.setup()
    renderSpaceModal()

    expect(screen.getByRole('dialog', { name: 'إضافة مساحة' })).toHaveAccessibleDescription('ستُضاف ضمن Level 1.')
    await user.click(screen.getByRole('button', { name: 'إضافة' }))

    expect(screen.getByLabelText(/^الاسم/)).toHaveAccessibleDescription('الاسم مطلوب.')
    expect(screen.getByLabelText(/السعة/)).toHaveAccessibleDescription('يجب أن تكون السعة عددًا موجبًا.')
  })
})

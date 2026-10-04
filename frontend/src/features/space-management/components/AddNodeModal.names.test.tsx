// @vitest-environment jsdom
import { beforeAll, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { configureLanguages } from '@/i18n'
import { createFloor } from '@/features/space-management/api/spaceManagementApi'
import { AddNodeModal } from './AddNodeModal'

vi.mock('@/features/space-management/api/spaceManagementApi', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/features/space-management/api/spaceManagementApi')>()),
  createFloor: vi.fn().mockResolvedValue({}),
}))

configureLanguages({
  languages: [
    { code: 'en', name: 'English' },
    { code: 'ar', name: 'العربية' },
  ],
  defaultLanguage: 'en',
})

// Radix Select calls these, which jsdom doesn't have.
beforeAll(() => {
  Element.prototype.hasPointerCapture ??= () => false
  Element.prototype.releasePointerCapture ??= () => {}
  Element.prototype.scrollIntoView ??= () => {}
})

describe('AddNodeModal names', () => {
  it('sends the name in each language typed', async () => {
    const user = userEvent.setup()
    render(
      <AddNodeModal
        state={{ kind: 'floor', parentName: 'HQ', parentId: 'b1' }}
        token="t"
        spaceTypes={[]}
        onClose={vi.fn()}
        onCreated={vi.fn()}
        onError={vi.fn()}
      />,
    )

    await user.type(screen.getByLabelText(/^Name/), 'Level 2')
    await user.click(screen.getByRole('combobox', { name: 'Language of the name' }))
    await user.click(screen.getByRole('option', { name: /العربية/ }))
    await user.type(screen.getByLabelText(/^Name/), 'الطابق الثاني')
    await user.click(screen.getByRole('button', { name: 'Add' }))

    expect(createFloor).toHaveBeenCalledWith('t', {
      buildingId: 'b1',
      names: [
        { language: 'en', name: 'Level 2' },
        { language: 'ar', name: 'الطابق الثاني' },
      ],
      floorNumber: null,
    })
  })

  it('asks for the English name even when only the Arabic one was typed', async () => {
    const user = userEvent.setup()
    render(
      <AddNodeModal
        state={{ kind: 'floor', parentName: 'HQ', parentId: 'b1' }}
        token="t"
        spaceTypes={[]}
        onClose={vi.fn()}
        onCreated={vi.fn()}
        onError={vi.fn()}
      />,
    )

    await user.click(screen.getByRole('combobox', { name: 'Language of the name' }))
    await user.click(screen.getByRole('option', { name: /العربية/ }))
    await user.type(screen.getByLabelText(/^Name/), 'الطابق الثاني')
    await user.click(screen.getByRole('button', { name: 'Add' }))

    const name = screen.getByLabelText(/^Name/)
    expect(name).toHaveAccessibleDescription('Name is required.')
    // Switched back to English, where the name is missing.
    expect(name).toHaveAttribute('lang', 'en')
  })
})

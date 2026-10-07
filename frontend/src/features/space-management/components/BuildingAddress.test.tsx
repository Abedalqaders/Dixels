// @vitest-environment jsdom
import { beforeAll, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { configureLanguages } from '@/i18n'
import { createBuilding, updateBuilding } from '@/features/space-management/api/spaceManagementApi'
import { AddNodeModal } from './AddNodeModal'
import { EditDetailsModal } from './EditDetailsModal'

vi.mock('@/features/space-management/api/spaceManagementApi', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/features/space-management/api/spaceManagementApi')>()),
  createBuilding: vi.fn().mockResolvedValue({}),
  updateBuilding: vi.fn().mockResolvedValue({}),
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

async function switchTo(user: ReturnType<typeof userEvent.setup>, language: RegExp) {
  await user.click(screen.getByRole('combobox', { name: 'Language of the name' }))
  await user.click(screen.getByRole('option', { name: language }))
}

describe('building address', () => {
  it('sends an address per language, typed beside each name', async () => {
    const user = userEvent.setup()
    render(<AddNodeModal state={{ kind: 'building' }} token="t" spaceTypes={[]} onClose={vi.fn()} onCreated={vi.fn()} onError={vi.fn()} />)

    await user.type(screen.getByLabelText(/^Name/), 'HQ')
    await user.type(screen.getByLabelText(/^Address/), '12 King St, Amman')
    await switchTo(user, /العربية/)
    await user.type(screen.getByLabelText(/^Name/), 'المقر')
    await user.type(screen.getByLabelText(/^Address/), 'شارع الملك 12، عمّان')
    await user.click(screen.getByRole('button', { name: 'Add' }))

    expect(createBuilding).toHaveBeenCalledWith(
      't',
      expect.objectContaining({
        names: [
          { language: 'en', name: 'HQ' },
          { language: 'ar', name: 'المقر' },
        ],
        addresses: [
          { language: 'en', address: '12 King St, Amman' },
          { language: 'ar', address: 'شارع الملك 12، عمّان' },
        ],
      }),
    )
  })

  it('keeps the address box shut until that language has a name', async () => {
    const user = userEvent.setup()
    render(<AddNodeModal state={{ kind: 'building' }} token="t" spaceTypes={[]} onClose={vi.fn()} onCreated={vi.fn()} onError={vi.fn()} />)

    const address = screen.getByLabelText(/^Address/)
    expect(address).toBeDisabled()
    expect(address).toHaveAccessibleDescription(
      'Give it a name in English first, then add the English address. Change the language next to the name to add the address in another language.',
    )

    await user.type(screen.getByLabelText(/^Name/), 'HQ')
    expect(screen.getByLabelText(/^Address/)).toBeEnabled()
  })

  it("names the language it types in, follows the name's dropdown and marks the languages with an address", async () => {
    const user = userEvent.setup()
    render(<AddNodeModal state={{ kind: 'building' }} token="t" spaceTypes={[]} onClose={vi.fn()} onCreated={vi.fn()} onError={vi.fn()} />)

    expect(screen.getByLabelText(/^Address/)).toHaveAccessibleName('Address (English) (optional)')
    expect(screen.getByText('Change the language next to the name to add the address in another language.')).toBeVisible()

    await user.type(screen.getByLabelText(/^Name/), 'HQ')
    await user.type(screen.getByLabelText(/^Address/), '12 King St')
    await user.click(screen.getByRole('combobox', { name: 'Language of the name' }))
    expect(screen.getByRole('option', { name: /English/ })).toHaveTextContent('· address')
    expect(screen.getByRole('option', { name: /العربية/ })).not.toHaveTextContent('· address')
    await user.click(screen.getByRole('option', { name: /العربية/ }))

    expect(screen.getByLabelText(/^Address/)).toHaveAccessibleName('Address (العربية) (optional)')
    expect(screen.getByLabelText(/^Address/)).toHaveValue('')
  })

  it('keeps the address when only the name is edited, and clears it when emptied', async () => {
    const user = userEvent.setup()
    const state = {
      kind: 'building' as const,
      id: 'b1',
      names: [{ language: 'en', name: 'HQ' }],
      addresses: [{ language: 'en', address: '12 King St' }],
      buildingNumber: null,
      timezone: 'UTC',
    }
    const { unmount } = render(<EditDetailsModal state={state} token="t" spaceTypes={[]} onClose={vi.fn()} onSaved={vi.fn()} onError={vi.fn()} />)

    expect(screen.getByLabelText(/^Address/)).toHaveValue('12 King St')
    await user.type(screen.getByLabelText(/^Name/), ' North')
    await user.click(screen.getByRole('button', { name: 'Save details' }))
    expect(updateBuilding).toHaveBeenLastCalledWith('t', 'b1', expect.objectContaining({
      names: [{ language: 'en', name: 'HQ North' }],
      addresses: [{ language: 'en', address: '12 King St' }],
    }))
    unmount()

    render(<EditDetailsModal state={state} token="t" spaceTypes={[]} onClose={vi.fn()} onSaved={vi.fn()} onError={vi.fn()} />)
    await user.clear(screen.getByLabelText(/^Address/))
    await user.click(screen.getByRole('button', { name: 'Save details' }))
    expect(updateBuilding).toHaveBeenLastCalledWith('t', 'b1', expect.objectContaining({ addresses: [] }))
  })
})

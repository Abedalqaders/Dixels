// @vitest-environment jsdom
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { configureLanguages } from '@/i18n'
import { ApiError, createSpaceType, updateSpaceType } from '@/features/space-management/api/spaceManagementApi'
import type { SpaceTypeDto } from '@/features/space-management/api/spaceManagementApi'
import { SpaceTypeFormDialog } from './SpaceTypeFormDialog'

vi.mock('@/features/space-management/api/spaceManagementApi', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/features/space-management/api/spaceManagementApi')>()),
  createSpaceType: vi.fn(),
  updateSpaceType: vi.fn(),
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

const phoneBooth: SpaceTypeDto = {
  id: 'st1',
  name: 'Phone booth',
  iconKey: 1,
  names: [
    { language: 'ar', name: 'كابينة هاتف' },
    { language: 'en', name: 'Phone booth' },
  ],
}

function renderDialog(spaceType: SpaceTypeDto | null = null) {
  const onSaved = vi.fn()
  render(<SpaceTypeFormDialog token="t" spaceType={spaceType} onClose={vi.fn()} onSaved={onSaved} />)
  return { onSaved }
}

const nameBox = () => screen.getByLabelText('Name')

async function pickLanguage(user: ReturnType<typeof userEvent.setup>, language: string) {
  await user.click(screen.getByRole('combobox', { name: 'Language of the name' }))
  await user.click(screen.getByRole('option', { name: new RegExp(language) }))
}

describe('SpaceTypeFormDialog', () => {
  beforeEach(() => {
    vi.mocked(createSpaceType).mockReset().mockResolvedValue(phoneBooth)
    vi.mocked(updateSpaceType).mockReset().mockResolvedValue(phoneBooth)
  })

  it('sends the name in every language typed', async () => {
    const user = userEvent.setup()
    const { onSaved } = renderDialog()

    await user.type(nameBox(), '  Phone booth ')
    await pickLanguage(user, 'العربية')
    await user.type(nameBox(), 'كابينة هاتف')
    await user.click(screen.getByRole('button', { name: 'Add type' }))

    expect(createSpaceType).toHaveBeenCalledWith('t', {
      names: [
        { language: 'en', name: 'Phone booth' },
        { language: 'ar', name: 'كابينة هاتف' },
      ],
      iconKey: 0,
    })
    expect(onSaved).toHaveBeenCalledWith('Space type added.')
  })

  it("can't be added without the English name", async () => {
    const user = userEvent.setup()
    renderDialog()

    await pickLanguage(user, 'العربية')
    await user.type(nameBox(), 'كابينة هاتف')

    expect(screen.getByRole('button', { name: 'Add type' })).toBeDisabled()
  })

  it('opens an existing type with its names, and saves nothing unchanged', async () => {
    const user = userEvent.setup()
    renderDialog(phoneBooth)

    expect(nameBox()).toHaveValue('Phone booth')
    await pickLanguage(user, 'العربية')
    expect(nameBox()).toHaveValue('كابينة هاتف')
    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled()
  })

  it('clearing a language removes its name', async () => {
    const user = userEvent.setup()
    renderDialog(phoneBooth)

    await pickLanguage(user, 'العربية')
    await user.clear(nameBox())
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(updateSpaceType).toHaveBeenCalledWith('t', 'st1', { names: [{ language: 'en', name: 'Phone booth' }], iconKey: 1 })
  })

  it('a duplicate switches the box to the language it clashes in, with the message under it', async () => {
    const user = userEvent.setup()
    vi.mocked(updateSpaceType).mockRejectedValue(
      new ApiError(403, {
        error: {
          code: 'Dixels:SpaceManagement:SpaceTypeNameAlreadyExists',
          message: "A space type named 'كشك' already exists.",
          data: { name: 'كشك', language: 'ar' },
        },
      }),
    )
    renderDialog(phoneBooth)

    await user.type(nameBox(), ' 2')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(await screen.findByText("A space type named 'كشك' already exists.")).toBeInTheDocument()
    expect(nameBox()).toHaveValue('كابينة هاتف')
    expect(nameBox()).toHaveAccessibleDescription("A space type named 'كشك' already exists.")
  })
})

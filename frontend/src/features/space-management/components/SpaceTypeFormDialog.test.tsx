// @vitest-environment jsdom
import { beforeEach, describe, expect, it, vi } from 'vitest'
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

describe('SpaceTypeFormDialog', () => {
  beforeEach(() => {
    vi.mocked(createSpaceType).mockReset().mockResolvedValue(phoneBooth)
    vi.mocked(updateSpaceType).mockReset().mockResolvedValue(phoneBooth)
  })

  it('sends the English name and every translation added', async () => {
    const user = userEvent.setup()
    const { onSaved } = renderDialog()

    await user.type(screen.getByLabelText('Name (English)'), '  Phone booth ')
    await user.click(screen.getByRole('button', { name: /Add translation/ }))
    await user.click(screen.getByRole('menuitem', { name: 'العربية' }))
    await user.type(screen.getByLabelText('العربية'), 'كابينة هاتف')
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

  it("doesn't send a translation row left empty", async () => {
    const user = userEvent.setup()
    renderDialog()

    await user.type(screen.getByLabelText('Name (English)'), 'Phone booth')
    await user.click(screen.getByRole('button', { name: /Add translation/ }))
    await user.click(screen.getByRole('menuitem', { name: 'العربية' }))
    await user.click(screen.getByRole('button', { name: 'Add type' }))

    expect(createSpaceType).toHaveBeenCalledWith('t', { names: [{ language: 'en', name: 'Phone booth' }], iconKey: 0 })
  })

  it('opens an existing type with each name in its field, and saves nothing unchanged', () => {
    renderDialog(phoneBooth)

    expect(screen.getByLabelText('Name (English)')).toHaveValue('Phone booth')
    expect(screen.getByLabelText('العربية')).toHaveValue('كابينة هاتف')
    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled()
  })

  it('counts a changed translation as a change, and sends every name', async () => {
    const user = userEvent.setup()
    renderDialog(phoneBooth)

    await user.clear(screen.getByLabelText('العربية'))
    await user.type(screen.getByLabelText('العربية'), 'كشك هاتف')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(updateSpaceType).toHaveBeenCalledWith('t', 'st1', {
      names: [
        { language: 'en', name: 'Phone booth' },
        { language: 'ar', name: 'كشك هاتف' },
      ],
      iconKey: 1,
    })
  })

  it('removing a translation saves the type without it', async () => {
    const user = userEvent.setup()
    renderDialog(phoneBooth)

    await user.click(screen.getByRole('button', { name: 'Remove the العربية name' }))
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(updateSpaceType).toHaveBeenCalledWith('t', 'st1', { names: [{ language: 'en', name: 'Phone booth' }], iconKey: 1 })
  })

  it('shows a duplicate name under the language it clashes in', async () => {
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

    await user.clear(screen.getByLabelText('العربية'))
    await user.type(screen.getByLabelText('العربية'), 'كشك')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    const arabic = await screen.findByLabelText('العربية')
    expect(arabic).toHaveAccessibleDescription("A space type named 'كشك' already exists.")
    expect(arabic).toHaveFocus()
    expect(screen.getByLabelText('Name (English)')).not.toHaveAttribute('aria-invalid')
  })

  it('shows an English duplicate under the English name', async () => {
    const user = userEvent.setup()
    vi.mocked(createSpaceType).mockRejectedValue(
      new ApiError(403, {
        error: {
          code: 'Dixels:SpaceManagement:SpaceTypeNameAlreadyExists',
          message: "A space type named 'Desk' already exists.",
          data: { name: 'Desk', language: 'en' },
        },
      }),
    )
    renderDialog()

    await user.type(screen.getByLabelText('Name (English)'), 'Desk')
    await user.click(screen.getByRole('button', { name: 'Add type' }))

    expect(await screen.findByLabelText('Name (English)')).toHaveAccessibleDescription("A space type named 'Desk' already exists.")
  })
})

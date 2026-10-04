// @vitest-environment jsdom
import { useState } from 'react'
import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { configureLanguages } from '@/i18n'
import { useFieldErrors } from './FieldError'
import { TranslationsField, translationFieldName } from './TranslationsField'
import type { TranslationValue } from './TranslationsField'

// Three languages, so "only the missing ones" means something. (Each test file has its
// own module state: this doesn't reach other files.)
configureLanguages({
  languages: [
    { code: 'en', name: 'English' },
    { code: 'ar', name: 'العربية' },
    { code: 'fr', name: 'Français' },
  ],
  defaultLanguage: 'en',
})

function Harness({ initial = [], errors = {} }: { initial?: TranslationValue[]; errors?: Record<string, string> }) {
  const [value, setValue] = useState(initial)
  const f = useFieldErrors<string>('t')
  return (
    <>
      <TranslationsField value={value} onChange={setValue} fieldErrors={f} />
      <button type="button" onClick={() => f.setErrors(errors)}>
        Show errors
      </button>
      <output data-testid="value">{JSON.stringify(value)}</output>
    </>
  )
}

const valueOf = () => JSON.parse(screen.getByTestId('value').textContent ?? '[]') as TranslationValue[]

describe('TranslationsField', () => {
  it('starts with no rows and counts none of the other languages', () => {
    render(<Harness />)

    expect(screen.queryByRole('textbox')).not.toBeInTheDocument()
    expect(screen.getByText('0 of 2')).toBeInTheDocument()
  })

  it('adds a language from the menu, ready to type in, in its own direction', async () => {
    const user = userEvent.setup()
    render(<Harness />)

    await user.click(screen.getByRole('button', { name: /Add translation/ }))
    await user.click(screen.getByRole('menuitem', { name: 'العربية' }))

    const arabic = screen.getByLabelText('العربية')
    expect(arabic).toHaveFocus()
    expect(arabic).toHaveAttribute('dir', 'rtl')
    expect(arabic).toHaveAttribute('lang', 'ar')
    await user.type(arabic, 'كابينة هاتف')

    expect(valueOf()).toEqual([{ language: 'ar', name: 'كابينة هاتف' }])
    expect(screen.getByText('1 of 2')).toBeInTheDocument()
  })

  it('offers only the languages not added yet — never the default one', async () => {
    const user = userEvent.setup()
    render(<Harness initial={[{ language: 'ar', name: 'مكتب' }]} />)

    await user.click(screen.getByRole('button', { name: /Add translation/ }))

    expect(screen.getAllByRole('menuitem').map((item) => item.textContent)).toEqual(['Français'])
  })

  it('hides the add button once every language has a row', () => {
    render(
      <Harness
        initial={[
          { language: 'ar', name: 'مكتب' },
          { language: 'fr', name: 'Bureau' },
        ]}
      />,
    )

    expect(screen.queryByRole('button', { name: /Add translation/ })).not.toBeInTheDocument()
    expect(screen.getByText('2 of 2')).toBeInTheDocument()
  })

  it('removes a row', async () => {
    const user = userEvent.setup()
    render(
      <Harness
        initial={[
          { language: 'ar', name: 'مكتب' },
          { language: 'fr', name: 'Bureau' },
        ]}
      />,
    )

    await user.click(screen.getByRole('button', { name: 'Remove the العربية name' }))

    expect(screen.queryByLabelText('العربية')).not.toBeInTheDocument()
    expect(valueOf()).toEqual([{ language: 'fr', name: 'Bureau' }])
    expect(screen.getByText('1 of 2')).toBeInTheDocument()
  })

  it('shows an error under the row it belongs to', async () => {
    const user = userEvent.setup()
    render(
      <Harness
        initial={[
          { language: 'ar', name: 'مكتب' },
          { language: 'fr', name: 'Bureau' },
        ]}
        errors={{ [translationFieldName('fr')]: 'Taken.' }}
      />,
    )

    await user.click(screen.getByRole('button', { name: 'Show errors' }))

    expect(screen.getByLabelText('Français')).toHaveAccessibleDescription('Taken.')
    expect(screen.getByLabelText('العربية')).not.toHaveAttribute('aria-invalid')
  })
})

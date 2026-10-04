// @vitest-environment jsdom
import { useState } from 'react'
import { beforeAll, describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { configureLanguages } from '@/i18n'
import { LocalizedNameField, fromNameList, languageWithForeignLetters, nameLettersProblem, toNameList } from './LocalizedNameField'
import type { LocalizedNames } from './LocalizedNameField'

// Three languages, so "missing" lists more than one. (Each test file has its own module
// state: this doesn't reach other files.)
configureLanguages({
  languages: [
    { code: 'en', name: 'English' },
    { code: 'ar', name: 'العربية' },
    { code: 'fr', name: 'Français' },
  ],
  defaultLanguage: 'en',
})

// Radix Select calls these, which jsdom doesn't have.
beforeAll(() => {
  Element.prototype.hasPointerCapture ??= () => false
  Element.prototype.releasePointerCapture ??= () => {}
  Element.prototype.scrollIntoView ??= () => {}
})

function Harness({ initial = {} }: { initial?: LocalizedNames }) {
  const [value, setValue] = useState(initial)
  const [language, setLanguage] = useState('en')
  return (
    <>
      <LocalizedNameField id="n" label="Name" value={value} onChange={setValue} language={language} onLanguageChange={setLanguage} />
      <output data-testid="value">{JSON.stringify(value)}</output>
    </>
  )
}

const valueOf = () => JSON.parse(screen.getByTestId('value').textContent ?? '{}') as LocalizedNames

async function pick(user: ReturnType<typeof userEvent.setup>, language: string) {
  await user.click(screen.getByRole('combobox', { name: 'Language of the name' }))
  await user.click(screen.getByRole('option', { name: new RegExp(language) }))
}

describe('LocalizedNameField', () => {
  it('edits the name of whichever language is picked, in one box', async () => {
    const user = userEvent.setup()
    render(<Harness />)

    await user.type(screen.getByLabelText('Name'), 'Phone booth')
    await pick(user, 'العربية')
    expect(screen.getByLabelText('Name')).toHaveValue('')
    await user.type(screen.getByLabelText('Name'), 'كابينة هاتف')

    expect(valueOf()).toEqual({ en: 'Phone booth', ar: 'كابينة هاتف' })

    await pick(user, 'English')
    expect(screen.getByLabelText('Name')).toHaveValue('Phone booth')
  })

  it("types in the picked language's own direction", async () => {
    const user = userEvent.setup()
    render(<Harness />)

    expect(screen.getByLabelText('Name')).toHaveAttribute('dir', 'ltr')
    await pick(user, 'العربية')

    expect(screen.getByLabelText('Name')).toHaveAttribute('dir', 'rtl')
    expect(screen.getByLabelText('Name')).toHaveAttribute('lang', 'ar')
  })

  it('says the default language is required until it has a name — and nothing about the others', async () => {
    const user = userEvent.setup()
    render(<Harness />)

    expect(screen.getByText(/A name in English is required/)).toBeInTheDocument()

    await user.type(screen.getByLabelText('Name'), 'Desk')

    expect(screen.queryByText(/required/)).not.toBeInTheDocument()
    expect(screen.queryByText(/العربية|Français/, { selector: 'p' })).not.toBeInTheDocument()
  })

  it('ticks the languages that have a name in the dropdown', async () => {
    const user = userEvent.setup()
    render(<Harness initial={{ en: 'Desk', fr: 'Bureau' }} />)

    await user.click(screen.getByRole('combobox', { name: 'Language of the name' }))

    const ticked = screen.getAllByRole('option').filter((o) => o.querySelector('[aria-label="has a name"]'))
    expect(ticked.map((o) => o.textContent)).toEqual(['Englishrequired', 'Français'])
  })

  it("says so as soon as a name has another language's letters, and marks that language", async () => {
    const user = userEvent.setup()
    render(<Harness initial={{ en: 'Desk' }} />)

    await pick(user, 'العربية')
    await user.type(screen.getByLabelText('Name'), 'Desk')

    expect(screen.getByLabelText('Name')).toHaveAttribute('aria-invalid', 'true')
    expect(screen.getByRole('alert')).toHaveTextContent(/Write the العربية name in its own letters/)

    await user.click(screen.getByRole('combobox', { name: 'Language of the name' }))
    const flagged = screen.getAllByRole('option').filter((o) => o.querySelector('[aria-label="Needs fixing"]'))
    expect(flagged.map((o) => o.textContent)).toEqual(['العربية'])
  })

  it('takes Arabic letters with short Latin codes in the Arabic name', async () => {
    const user = userEvent.setup()
    render(<Harness initial={{ en: 'IT room' }} />)

    await pick(user, 'العربية')
    await user.type(screen.getByLabelText('Name'), 'غرفة IT')

    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('turns into the API list and back, without empty names', () => {
    expect(toNameList({ en: ' Desk ', ar: '  ', fr: 'Bureau' })).toEqual([
      { language: 'en', name: 'Desk' },
      { language: 'fr', name: 'Bureau' },
    ])
    expect(fromNameList([{ language: 'ar', name: 'مكتب' }])).toEqual({ ar: 'مكتب' })
  })
})

describe('nameLettersProblem', () => {
  // The same cases as NameAlphabetTests on the backend: the two must agree.
  it.each([
    ['en', 'Meeting room 101'],
    ['en', 'Café (north)'],
    ['en', '101'],
    ['ar', 'غرفة اجتماعات'],
    ['ar', 'غرفة ١٠١'],
    ['ar', 'مكتـــب'],
    ['ar', 'غرفة IT'],
    ['ar', 'غرفة B12'],
    ['ar', 'كابينة 3-01'],
    ['ar', '101'],
    ['ar-JO', 'قاعة VIP'],
  ])('%s: "%s" fits', (language, name) => {
    expect(nameLettersProblem(language, name)).toBeNull()
  })

  it.each([
    ['en', 'غرفة'],
    ['en', 'Room غرفة'],
    ['en', 'Room IT غرفة'],
    ['ar', 'Meeting room'],
    ['ar', 'غرفة meeting'],
    ['ar', 'غرفة MEETING'],
    ['ar', 'غرفةIT'],
    ['ar', 'IT'],
    ['ar', 'B12'],
  ])('%s: "%s" does not fit', (language, name) => {
    expect(nameLettersProblem(language, name)).not.toBeNull()
  })

  it('mentions codes only for languages not written in Latin letters', () => {
    expect(nameLettersProblem('en', 'غرفة')).toBe('Dixels:Localization:NameHasForeignLetters')
    expect(nameLettersProblem('ar', 'Room')).toBe('Dixels:Localization:NameHasForeignLettersExceptCodes')
  })

  it('names the first language that needs fixing, for the form to switch to', () => {
    expect(languageWithForeignLetters({ en: 'Desk', ar: 'مكتب' })).toBeUndefined()
    expect(languageWithForeignLetters({ en: 'Desk', ar: 'Desk' })).toBe('ar')
  })
})

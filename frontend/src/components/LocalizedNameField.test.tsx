// @vitest-environment jsdom
import { useState } from 'react'
import { beforeAll, describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { configureLanguages } from '@/i18n'
import { LocalizedNameField, fromNameList, toNameList } from './LocalizedNameField'
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

  it('says the default language is required until it has a name, then which are missing', async () => {
    const user = userEvent.setup()
    render(<Harness />)

    expect(screen.getByText(/A name in English is required/)).toBeInTheDocument()

    await user.type(screen.getByLabelText('Name'), 'Desk')

    expect(screen.getByText('Missing: العربية, Français')).toBeInTheDocument()
  })

  it('ticks the languages that have a name in the dropdown', async () => {
    const user = userEvent.setup()
    render(<Harness initial={{ en: 'Desk', fr: 'Bureau' }} />)

    await user.click(screen.getByRole('combobox', { name: 'Language of the name' }))

    const ticked = screen.getAllByRole('option').filter((o) => o.querySelector('[aria-label="has a name"]'))
    expect(ticked.map((o) => o.textContent)).toEqual(['Englishrequired', 'Français'])
  })

  it('turns into the API list and back, without empty names', () => {
    expect(toNameList({ en: ' Desk ', ar: '  ', fr: 'Bureau' })).toEqual([
      { language: 'en', name: 'Desk' },
      { language: 'fr', name: 'Bureau' },
    ])
    expect(fromNameList([{ language: 'ar', name: 'مكتب' }])).toEqual({ ar: 'مكتب' })
  })
})

// @vitest-environment jsdom
import { useState } from 'react'
import { beforeAll, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { setLanguage } from '@/i18n'
import { TestProviders } from '@/test/providers'
import { PeoplePicker } from './PeoplePicker'
import type { PersonMatch, PickedPerson } from './PeoplePicker'

const COLLEAGUES: PersonMatch[] = [
  { id: 'u-sami', name: 'Sami Haddad', email: 'sami@dixels.io' },
  { id: 'u-sara', name: 'Sara Ali', email: 'sara@dixels.io' },
]

// cmdk scrolls the picked match into view and watches the list's size; jsdom has neither.
beforeAll(() => {
  Element.prototype.scrollIntoView ??= () => {}
  globalThis.ResizeObserver ??= class {
    observe() {}
    unobserve() {}
    disconnect() {}
  }
})

function Harness({ initial = [], allowGuests = true, search }: { initial?: PickedPerson[]; allowGuests?: boolean; search: (filter: string) => Promise<PersonMatch[]> }) {
  const [people, setPeople] = useState(initial)
  return (
    <PeoplePicker
      id="pp"
      value={people}
      onChange={setPeople}
      searchKey={(filter) => ['test-colleagues', filter]}
      search={search}
      allowGuests={allowGuests}
    />
  )
}

function setup(props: Partial<Parameters<typeof Harness>[0]> = {}) {
  const search = vi.fn((filter: string) => Promise.resolve(COLLEAGUES.filter((c) => c.name.toLowerCase().includes(filter.toLowerCase()))))
  render(<Harness search={search} {...props} />, { wrapper: TestProviders })
  return { user: userEvent.setup(), search }
}

const picked = () => within(screen.getByRole('list', { name: 'Invited' }))

describe('PeoplePicker', () => {
  it('searches from 2 letters, once the typing pauses', async () => {
    const { user, search } = setup()
    const box = screen.getByRole('combobox', { name: 'Search colleagues by name or email' })

    await user.type(box, 's')
    expect(await screen.findByText('Type at least 2 letters.')).toBeInTheDocument()
    expect(search).not.toHaveBeenCalled()

    await user.type(box, 'a')
    expect(await screen.findByRole('option', { name: /Sami Haddad/ })).toBeInTheDocument()
    expect(search).toHaveBeenCalledTimes(1)
    expect(search).toHaveBeenCalledWith('sa')
  })

  it('adds with the arrows and Enter, and leaves the added person out of the next search', async () => {
    const { user } = setup()
    const box = screen.getByRole('combobox', { name: 'Search colleagues by name or email' })

    await user.type(box, 'sa')
    await screen.findByRole('option', { name: /Sami Haddad/ })
    await user.keyboard('{ArrowDown}{Enter}')

    expect(picked().getByText('Sara Ali')).toBeInTheDocument()
    expect(box).toHaveValue('')

    await user.type(box, 'sa')
    await screen.findByRole('option', { name: /Sami Haddad/ })
    expect(screen.queryByRole('option', { name: /Sara Ali/ })).not.toBeInTheDocument()
  })

  it('removes the last person with Backspace in an empty search', async () => {
    const { user } = setup({ initial: [{ userId: 'u-sara', name: 'Sara Ali', email: 'sara@dixels.io', isExternal: false }] })

    await user.click(screen.getByRole('combobox', { name: 'Search colleagues by name or email' }))
    await user.keyboard('{Backspace}')

    expect(screen.queryByRole('list', { name: 'Invited' })).not.toBeInTheDocument()
  })

  it('adds an outside guest by email and name, tagged "Guest"', async () => {
    const { user } = setup()

    await user.click(screen.getByRole('tab', { name: 'External guest' }))
    await user.type(screen.getByLabelText('Email'), 'omar@acme')
    await user.click(screen.getByRole('button', { name: 'Add guest' }))
    expect(screen.getByLabelText('Email')).toHaveAccessibleDescription('Enter a full email, like name@company.com.')

    await user.type(screen.getByLabelText('Email'), '.com')
    await user.type(screen.getByLabelText('Name'), 'Omar Farouk{Enter}')

    const row = picked().getByText('Omar Farouk').closest('li')!
    expect(within(row).getByText('omar@acme.com')).toBeInTheDocument()
    expect(within(row).getByText('Guest')).toBeInTheDocument()
    expect(screen.getByLabelText('Email')).toHaveValue('')
  })

  it("won't add the same email twice, whatever its case", async () => {
    const { user } = setup({ initial: [{ userId: null, name: '', email: 'omar@acme.com', isExternal: true }] })

    await user.click(screen.getByRole('tab', { name: 'External guest' }))
    await user.type(screen.getByLabelText('Email'), 'Omar@Acme.com{Enter}')

    expect(screen.getByLabelText('Email')).toHaveAccessibleDescription('This person is already invited.')
    expect(picked().getAllByRole('listitem')).toHaveLength(1)
  })

  it('has no "External guest" tab while outside guests are switched off', () => {
    setup({ allowGuests: false })
    expect(screen.queryByRole('tab')).not.toBeInTheDocument()
    expect(screen.getByRole('combobox', { name: 'Search colleagues by name or email' })).toBeInTheDocument()
  })

  it('speaks Arabic', async () => {
    await setLanguage('ar')
    const { user } = setup()

    await user.click(screen.getByRole('tab', { name: 'ضيف خارجي' }))
    expect(screen.getByLabelText('البريد الإلكتروني')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'إضافة ضيف' })).toBeInTheDocument()
    await waitFor(() => expect(screen.getByRole('tab', { name: 'ضيف خارجي' })).toHaveAttribute('aria-selected', 'true'))
  })
})

// @vitest-environment jsdom
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createBooking, previewBooking } from '@/features/bookings/api/bookingsApi'
import type { BookableBuildingDto, BookableSpaceDto, BookingDto, BookingPreviewDto } from '@/features/bookings/api/bookingsApi'
import { getExternalGuestsEnabled, searchColleagues } from '@/features/bookings/api/inviteesApi'
import { TestProviders } from '@/test/providers'
import { BookingForm } from './BookingForm'

vi.mock('@/features/bookings/api/bookingsApi', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/features/bookings/api/bookingsApi')>()
  // The room's days stay loading: the plain time limits apply.
  return { ...actual, previewBooking: vi.fn(), createBooking: vi.fn(), getSpaceDays: vi.fn(() => new Promise(() => {})) }
})

vi.mock('@/features/bookings/api/inviteesApi', () => ({
  searchColleagues: vi.fn(),
  getExternalGuestsEnabled: vi.fn(),
}))

const space: BookableSpaceDto = {
  id: 'space-1',
  name: 'Room 1',
  spaceTypeId: 'type-1',
  spaceTypeName: 'Meeting room',
  iconKey: 0,
  capacity: 8,
  minAttendees: null,
  days: { value: [0, 1, 2, 3, 4, 5, 6], source: 'Building' },
  hours: { value: { isOpen24Hours: true, open: '00:00', close: '00:00' }, source: 'Building' },
  maxDurationMinutes: { value: 120, source: 'Building' },
}

const building: BookableBuildingDto = {
  id: 'b-1',
  name: 'Test HQ',
  timezone: 'UTC',
  maxHorizonDays: 30,
  maxSeriesHorizonDays: 90,
  isRemoved: false,
  minLeadMinutes: 0,
  slotMinutes: 15,
  ownOverlapPolicy: 1,
  days: [0, 1, 2, 3, 4, 5, 6],
  hours: { isOpen24Hours: false, open: '07:00', close: '20:00' },
  floors: [{ id: 'f-1', name: 'Level 1', floorNumber: 1, spaces: [space] }],
}

const valid: BookingPreviewDto = { isValid: true, violations: [], warnings: [], startsAt: '', endsAt: '', timezone: 'UTC', invitees: [] }

// cmdk scrolls the picked match into view and watches the list's size; jsdom has neither.
beforeAll(() => {
  Element.prototype.scrollIntoView ??= () => {}
  globalThis.ResizeObserver ??= class {
    observe() {}
    unobserve() {}
    disconnect() {}
  }
})

function renderForm(onBooked = vi.fn()) {
  render(<BookingForm token="t" building={building} space={space} floorName="Level 1" onClose={vi.fn()} onBooked={onBooked} />, {
    wrapper: TestProviders,
  })
  return onBooked
}

const lastPreview = () => vi.mocked(previewBooking).mock.lastCall![1]

async function inviteSara(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByRole('combobox', { name: 'Search colleagues by name or email' }), 'sa')
  await user.click(await screen.findByRole('option', { name: /Sara Ali/ }))
}

describe('BookingForm invitees', () => {
  beforeEach(() => {
    vi.mocked(previewBooking).mockReset().mockResolvedValue(valid)
    vi.mocked(createBooking).mockReset()
    vi.mocked(getExternalGuestsEnabled).mockReset().mockResolvedValue(true)
    vi.mocked(searchColleagues).mockReset().mockResolvedValue([{ id: 'u-sara', name: 'Sara Ali', email: 'sara@dixels.io' }])
  })

  it('counts you plus each guest as they come and go, and checks the guests with the slot', async () => {
    const user = userEvent.setup()
    renderForm()
    const people = screen.getByRole('group', { name: 'People' })
    expect(people).toHaveTextContent('1 person · just you')

    await inviteSara(user)

    expect(people).toHaveTextContent('2 people · you + 1 guest')
    await waitFor(() => expect(lastPreview()).toMatchObject({ invitees: [{ userId: 'u-sara' }] }))
    expect(lastPreview()).not.toHaveProperty('attendees')

    await user.click(screen.getByRole('button', { name: 'Remove Sara Ali' }))
    expect(people).toHaveTextContent('1 person · just you')
  })

  it("shows a guest's email that belongs to a colleague as that colleague once the server says so", async () => {
    vi.mocked(previewBooking).mockImplementation(async (_token, input) => {
      const invitees = input.invitees ?? []
      return {
        ...valid,
        invitees: invitees.map(() => ({ userId: 'u-sara', name: 'Sara Ali', email: 'sara@dixels.io', isExternal: false, responseStatus: 0 as const, isBusy: false, busyDates: 0, busyTimes: [] })),
      }
    })
    const user = userEvent.setup()
    renderForm()

    await user.click(await screen.findByRole('tab', { name: 'External guest' }))
    await user.type(screen.getByLabelText('Email'), 'sara@dixels.io{Enter}')

    const list = screen.getByRole('list', { name: 'Invited' })
    await waitFor(() => expect(within(list).getByText('Sara Ali')).toBeInTheDocument())
    expect(within(list).queryByText('Guest')).not.toBeInTheDocument()
    await waitFor(() => expect(lastPreview().invitees).toEqual([{ userId: 'u-sara' }]))
  })

  it('books with the guests', async () => {
    const booking = { id: 'bk-1' } as BookingDto
    vi.mocked(createBooking).mockResolvedValue(booking)
    const user = userEvent.setup()
    const onBooked = renderForm()

    await inviteSara(user)
    await user.click(await screen.findByRole('tab', { name: 'External guest' }))
    await user.type(screen.getByLabelText('Email'), 'omar@acme.com')
    await user.type(screen.getByLabelText('Name'), 'Omar{Enter}')
    await waitFor(() => expect(screen.getByRole('button', { name: 'Book' })).toBeEnabled())
    await user.click(screen.getByRole('button', { name: 'Book' }))

    await waitFor(() => expect(onBooked).toHaveBeenCalledWith(booking))
    expect(screen.getByRole('group', { name: 'People' })).toHaveTextContent('3 people · you + 2 guests')
    expect(vi.mocked(createBooking).mock.calls[0][1]).toMatchObject({
      invitees: [{ userId: 'u-sara' }, { email: 'omar@acme.com', name: 'Omar' }],
    })
  })

  it('shows a busy colleague Teams-style — red ring, busy times, one line above — and still books with them', async () => {
    vi.mocked(previewBooking).mockImplementation(async (_token, input) => ({
      ...valid,
      invitees: (input.invitees ?? []).map(() => ({
        userId: 'u-sara', name: 'Sara Ali', email: 'sara@dixels.io', isExternal: false, responseStatus: 0 as const,
        isBusy: true, busyDates: 1, busyTimes: [{ localStart: '2027-01-10T10:00:00', localEnd: '2027-01-10T10:30:00' }],
      })),
    }))
    const user = userEvent.setup()
    renderForm()

    await inviteSara(user)

    const list = screen.getByRole('list', { name: 'Invited' })
    const pill = await within(list).findByRole('button', { name: /^Busy \W?10:00\W?–\W?10:30\W?$/ })
    expect(list.querySelector('[data-presence="busy"]')).not.toBeNull()
    expect(await screen.findByText(/^1 person is busy at/)).toBeInTheDocument()

    await user.click(pill)
    expect(await screen.findByText('Busy times')).toBeInTheDocument()
    expect(screen.getByText('Times only. Meeting details stay private.')).toBeInTheDocument()
    await waitFor(() => expect(screen.getByRole('button', { name: 'Book' })).toBeEnabled())
  })

  it('offers no outside guests while they are switched off', async () => {
    vi.mocked(getExternalGuestsEnabled).mockResolvedValue(false)
    renderForm()
    await screen.findByText('Available')
    expect(screen.queryByRole('tab', { name: 'External guest' })).not.toBeInTheDocument()
  })
})

// @vitest-environment jsdom
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ApiError, getBusyGuests, getSeriesBusyGuests, updateInvitees, updateSeriesInvitees } from '@/features/bookings/api/bookingsApi'
import type { BookingDto } from '@/features/bookings/api/bookingsApi'
import { getExternalGuestsEnabled, searchColleagues } from '@/features/bookings/api/inviteesApi'
import { TestProviders } from '@/test/providers'
import { EditGuestsDialog } from './EditGuestsDialog'

vi.mock('@/features/bookings/api/bookingsApi', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/features/bookings/api/bookingsApi')>()
  return { ...actual, updateInvitees: vi.fn(), updateSeriesInvitees: vi.fn(), getBusyGuests: vi.fn(), getSeriesBusyGuests: vi.fn() }
})

vi.mock('@/features/bookings/api/inviteesApi', () => ({
  searchColleagues: vi.fn(),
  getExternalGuestsEnabled: vi.fn(),
}))

// cmdk scrolls the picked match into view and watches the list's size; jsdom has neither.
beforeAll(() => {
  Element.prototype.scrollIntoView ??= () => {}
  globalThis.ResizeObserver ??= class {
    observe() {}
    unobserve() {}
    disconnect() {}
  }
})

const rana = { userId: 'u-rana', name: 'Rana Saleh', email: 'rana@dixels.io', isExternal: false, responseStatus: 0 as const, isBusy: false, busyDates: 0, maybeBusyDates: 0, busyTimes: [] }
const omar = { ...rana, userId: 'u-omar', name: 'Omar Haddad', email: 'omar@dixels.io' }

function booking(patch: Partial<BookingDto> = {}): BookingDto {
  return {
    id: 'b-1',
    spaceId: 's-1',
    spaceName: 'Room 1',
    floorName: 'Level 1',
    buildingName: 'HQ',
    timezone: 'UTC',
    startsAt: '2026-10-08T10:00:00Z',
    endsAt: '2026-10-08T11:00:00Z',
    localStart: '2026-10-08T10:00:00',
    localEnd: '2026-10-08T11:00:00',
    attendees: 2,
    title: 'Planning',
    status: 'Confirmed',
    seriesId: null,
    recurrence: null,
    cancelledByAdmin: false,
    cancelReason: null,
    invitees: [rana],
    isOwner: true,
    ownerName: 'Me',
    myBusy: [],
    capacity: 6,
    minAttendees: null,
    myResponse: null,
    ...patch,
  }
}

function renderDialog(b: BookingDto, onSaved = vi.fn()) {
  render(<EditGuestsDialog token="t" booking={b} onClose={vi.fn()} onSaved={onSaved} />, { wrapper: TestProviders })
  return onSaved
}

const people = () => screen.getByRole('group', { name: 'People' })
const save = () => screen.getByRole('button', { name: 'Save' })

async function inviteOmar(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByRole('combobox', { name: 'Search colleagues by name or email' }), 'om')
  await user.click(await screen.findByRole('option', { name: /Omar Haddad/ }))
}

describe('EditGuestsDialog', () => {
  beforeEach(() => {
    vi.mocked(updateInvitees).mockReset().mockResolvedValue(booking())
    vi.mocked(updateSeriesInvitees).mockReset().mockResolvedValue({ seriesId: 'series-1', bookings: [] })
    vi.mocked(getExternalGuestsEnabled).mockReset().mockResolvedValue(false)
    vi.mocked(getBusyGuests).mockReset().mockResolvedValue({ dates: 1, items: [] })
    vi.mocked(getSeriesBusyGuests).mockReset().mockResolvedValue({ dates: 1, items: [] })
    vi.mocked(searchColleagues).mockReset().mockResolvedValue([{ id: 'u-omar', name: 'Omar Haddad', email: 'omar@dixels.io' }])
  })

  it('starts from the booking, raises the head count for a new guest, and saves the list', async () => {
    const user = userEvent.setup()
    const onSaved = renderDialog(booking())
    expect(screen.getByText('Rana Saleh')).toBeInTheDocument()
    expect(people()).toHaveTextContent('2 people · you + 1 guest')

    await inviteOmar(user)
    expect(people()).toHaveTextContent('3 people · you + 2 guests')

    await user.click(save())

    await waitFor(() => expect(onSaved).toHaveBeenCalled())
    expect(updateInvitees).toHaveBeenCalledWith('t', 'b-1', { invitees: [{ userId: 'u-rana' }, { userId: 'u-omar' }] })
    expect(updateSeriesInvitees).not.toHaveBeenCalled()
  })

  it('lets a booking kept over a shrunk room lose people, but not grow past it', async () => {
    const user = userEvent.setup()
    // Kept at 6 (one guest named) when the room shrank to 4: taking Rana off is fine.
    const { unmount } = render(<EditGuestsDialog token="t" booking={booking({ attendees: 6, capacity: 4 })} onClose={vi.fn()} onSaved={vi.fn()} />, {
      wrapper: TestProviders,
    })
    await user.click(screen.getByRole('button', { name: 'Remove Rana Saleh' }))
    expect(people()).toHaveTextContent('1 person · just you')
    expect(save()).toBeEnabled()
    unmount()

    // Full at 2 in a 2-seat room: one more guest doesn't fit.
    renderDialog(booking({ attendees: 2, capacity: 2 }))
    await inviteOmar(user)
    expect(screen.getByText('This room seats 2.')).toBeInTheDocument()
    expect(save()).toBeDisabled()
  })

  it('says how many to invite when removing guests drops below the room minimum, but not when already under it', async () => {
    const user = userEvent.setup()
    // Kept at 3 (Rana and Omar) when the room's minimum rose to 5: still fine as it is.
    renderDialog(booking({ attendees: 3, minAttendees: 5, invitees: [rana, omar] }))
    expect(screen.queryByText(/This room needs at least/)).not.toBeInTheDocument()
    expect(save()).toBeEnabled()

    await user.click(screen.getByRole('button', { name: 'Remove Omar Haddad' }))
    expect(screen.getByText('This room needs at least 5 people: invite 3 more.')).toBeInTheDocument()
    expect(save()).toBeDisabled()
  })

  it('on a series, says it changes every upcoming date and saves the series', async () => {
    const user = userEvent.setup()
    const onSaved = renderDialog(booking({ seriesId: 'series-1' }))

    expect(screen.getByText(/Applies to all upcoming dates of this series/)).toBeInTheDocument()
    await user.click(save())

    await waitFor(() => expect(onSaved).toHaveBeenCalled())
    expect(updateSeriesInvitees).toHaveBeenCalledWith('t', 'series-1', { invitees: [{ userId: 'u-rana' }] })
  })

  it('asks who of the listed colleagues is busy, and shows when', async () => {
    vi.mocked(getBusyGuests).mockResolvedValue({
      dates: 1,
      items: [{ userId: 'u-rana', busyDates: 1, maybeBusyDates: 0, times: [{ localStart: '2026-10-08T10:00:00', localEnd: '2026-10-08T11:00:00', isTentative: false }] }],
    })
    renderDialog(booking())

    expect(await screen.findByRole('button', { name: /^Busy \W?10:00\W?–\W?11:00\W?$/ })).toBeInTheDocument()
    expect(screen.getByText('1 person is busy at 10:00')).toBeInTheDocument()
    expect(document.querySelector('[data-presence="busy"]')).not.toBeNull()
    expect(getBusyGuests).toHaveBeenCalledWith('t', 'b-1', ['u-rana'])
    expect(save()).toBeEnabled()
  })

  it('shows a free colleague in green, with no summary line', async () => {
    renderDialog(booking())

    expect(await screen.findByText('Free')).toBeInTheDocument()
    expect(document.querySelector('[data-presence="free"]')).not.toBeNull()
    expect(screen.queryByText(/is busy at/)).not.toBeInTheDocument()
  })

  it('on a series, a half ring, "busy on 2 of 8 dates" and the dates with their times', async () => {
    const user = userEvent.setup()
    vi.mocked(getSeriesBusyGuests).mockResolvedValue({
      dates: 8,
      items: [{
        userId: 'u-rana',
        busyDates: 2,
        maybeBusyDates: 0,
        times: [
          { localStart: '2026-10-13T10:00:00', localEnd: '2026-10-13T10:30:00', isTentative: false },
          { localStart: '2026-10-20T10:00:00', localEnd: '2026-10-20T11:00:00', isTentative: false },
        ],
      }],
    })
    renderDialog(booking({ seriesId: 'series-1' }))

    const pill = await screen.findByRole('button', { name: 'Busy on 2 of 8 dates' })
    expect(screen.getByText('Someone is busy on 2 of 8 dates')).toBeInTheDocument()
    expect(document.querySelector('[data-presence="part"]')).not.toBeNull()
    expect(getSeriesBusyGuests).toHaveBeenCalledWith('t', 'series-1', ['u-rana'])

    await user.click(pill)
    expect(await screen.findByText('Busy on these dates')).toBeInTheDocument()
    expect(screen.getByText(/^\W?10:00\W?–\W?10:30\W?$/)).toBeInTheDocument()
    expect(screen.getByText(/^\W?10:00\W?–\W?11:00\W?$/)).toBeInTheDocument()
  })

  it("shows the server's answer under the head count when it's about it", async () => {
    const user = userEvent.setup()
    vi.mocked(updateInvitees).mockRejectedValue(
      new ApiError(400, { error: { code: 'Dixels:Bookings:OverCapacity', message: "This space seats 6, but with your guests you're 7 people." } }),
    )
    const onSaved = renderDialog(booking())

    await user.click(save())

    expect(await within(people()).findByText("This space seats 6, but with your guests you're 7 people.")).toBeInTheDocument()
    expect(onSaved).not.toHaveBeenCalled()
  })
})

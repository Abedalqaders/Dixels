// @vitest-environment jsdom
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
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

const rana = { userId: 'u-rana', name: 'Rana Saleh', email: 'rana@dixels.io', isExternal: false, responseStatus: 0 as const, isBusy: false, busyDates: 0 }

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
    capacity: 6,
    minAttendees: null,
    ...patch,
  }
}

function renderDialog(b: BookingDto, onSaved = vi.fn()) {
  render(<EditGuestsDialog token="t" booking={b} onClose={vi.fn()} onSaved={onSaved} />, { wrapper: TestProviders })
  return onSaved
}

const attendees = () => screen.getByLabelText('Attendees')
const save = () => screen.getByRole('button', { name: 'Save' })

async function typeAttendees(user: ReturnType<typeof userEvent.setup>, value: string) {
  await user.clear(attendees())
  await user.type(attendees(), value)
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
    expect(attendees()).toHaveValue(2)

    await user.type(screen.getByRole('combobox', { name: 'Search colleagues by name or email' }), 'om')
    await user.click(await screen.findByRole('option', { name: /Omar Haddad/ }))
    expect(attendees()).toHaveValue(3)

    await user.click(save())

    await waitFor(() => expect(onSaved).toHaveBeenCalled())
    expect(updateInvitees).toHaveBeenCalledWith('t', 'b-1', { attendees: 3, invitees: [{ userId: 'u-rana' }, { userId: 'u-omar' }] })
    expect(updateSeriesInvitees).not.toHaveBeenCalled()
  })

  it('keeps Save off while the head count leaves no room for everyone', async () => {
    const user = userEvent.setup()
    renderDialog(booking())

    await typeAttendees(user, '1')

    expect(screen.getByText('Needs at least 2 attendees for everyone invited')).toBeInTheDocument()
    expect(save()).toBeDisabled()
  })

  it('refuses a higher number than the room seats, but not one already over it', async () => {
    const user = userEvent.setup()
    // Kept at 6 when the room shrank to 4.
    renderDialog(booking({ attendees: 6, capacity: 4 }))

    await user.click(screen.getByRole('button', { name: 'Remove Rana Saleh' }))
    expect(save()).toBeEnabled()
    await typeAttendees(user, '5')
    expect(save()).toBeEnabled()

    await typeAttendees(user, '7')
    expect(screen.getByText('This room seats 4.')).toBeInTheDocument()
    expect(save()).toBeDisabled()
  })

  it('refuses a lower number than the room needs, but not one already under it', async () => {
    const user = userEvent.setup()
    // Kept at 3 when the room's minimum rose to 5.
    renderDialog(booking({ attendees: 3, minAttendees: 5 }))

    await typeAttendees(user, '4')
    expect(save()).toBeEnabled()

    await typeAttendees(user, '2')
    expect(screen.getByText('Needs at least 5 people')).toBeInTheDocument()
    expect(save()).toBeDisabled()
  })

  it('on a series, says it changes every upcoming date and saves the series', async () => {
    const user = userEvent.setup()
    const onSaved = renderDialog(booking({ seriesId: 'series-1' }))

    expect(screen.getByText(/Applies to all upcoming dates of this series/)).toBeInTheDocument()
    await user.click(save())

    await waitFor(() => expect(onSaved).toHaveBeenCalled())
    expect(updateSeriesInvitees).toHaveBeenCalledWith('t', 'series-1', { attendees: 2, invitees: [{ userId: 'u-rana' }] })
  })

  it('asks who of the listed colleagues is busy, and tags them', async () => {
    vi.mocked(getBusyGuests).mockResolvedValue({ dates: 1, items: [{ userId: 'u-rana', busyDates: 1 }] })
    renderDialog(booking())

    expect(await screen.findByText('Busy then')).toBeInTheDocument()
    expect(getBusyGuests).toHaveBeenCalledWith('t', 'b-1', ['u-rana'])
    expect(save()).toBeEnabled()
  })

  it('on a series, says on how many of its dates a colleague is busy', async () => {
    vi.mocked(getSeriesBusyGuests).mockResolvedValue({ dates: 8, items: [{ userId: 'u-rana', busyDates: 2 }] })
    renderDialog(booking({ seriesId: 'series-1' }))

    expect(await screen.findByText('Busy on 2 of 8 dates')).toBeInTheDocument()
    expect(getSeriesBusyGuests).toHaveBeenCalledWith('t', 'series-1', ['u-rana'])
  })

  it("shows the server's answer under Attendees when it's about the head count", async () => {
    const user = userEvent.setup()
    vi.mocked(updateInvitees).mockRejectedValue(
      new ApiError(400, { error: { code: 'Dixels:Bookings:OverCapacity', message: 'This space seats 6, but you asked for 3.' } }),
    )
    const onSaved = renderDialog(booking())

    await user.click(save())

    expect(await screen.findByText('This space seats 6, but you asked for 3.')).toBeInTheDocument()
    expect(onSaved).not.toHaveBeenCalled()
  })
})

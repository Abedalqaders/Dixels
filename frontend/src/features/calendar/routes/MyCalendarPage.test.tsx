// @vitest-environment jsdom
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { addDays, nowInZone } from '@/lib/time/buildingTime'
import { cancelBooking, getBooking, getMyBookableBuilding, getMyBookings, searchAvailability } from '@/features/bookings/api/bookingsApi'
import type { BookableBuildingDto, BookableSpaceDto, BookingDto } from '@/features/bookings/api/bookingsApi'
import { addMonths, gridMonthFor, monthGrid, startOfWeek } from '@/features/calendar/calendarDates'
import { Permissions } from '@/features/auth/permissions/permissionNames'
import type { PermissionsValue } from '@/features/auth/permissions/permissionsContext'
import { granted, WithPermissions } from '@/test/permissions'
import { TestProviders } from '@/test/providers'
import { MyCalendarPage } from './MyCalendarPage'

vi.mock('react-oidc-context', () => ({ useAuth: vi.fn() }))
vi.mock('@/features/bookings/api/bookingsApi', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/features/bookings/api/bookingsApi')>()
  return {
    ...actual,
    getMyBookableBuilding: vi.fn(),
    getMyBookings: vi.fn(),
    getBooking: vi.fn(),
    cancelBooking: vi.fn(),
    searchAvailability: vi.fn(),
    previewBooking: vi.fn(() => new Promise(() => {})),
  }
})

const room: BookableSpaceDto = {
  id: 's1',
  name: 'Meeting Room 301',
  spaceTypeId: 't1',
  spaceTypeName: 'Meeting room',
  iconKey: 0,
  capacity: 8,
  minAttendees: null,
  days: { value: [0, 1, 2, 3, 4, 5, 6], source: 'Building' },
  hours: { value: { isOpen24Hours: false, open: '07:00', close: '20:00' }, source: 'Building' },
  maxDurationMinutes: { value: 120, source: 'Building' },
}

const building: BookableBuildingDto = {
  id: 'b1',
  name: 'Riverside HQ',
  timezone: 'UTC',
  maxHorizonDays: 60,
  maxSeriesHorizonDays: 90,
  isRemoved: false,
  minLeadMinutes: 0,
  slotMinutes: 15,
  ownOverlapPolicy: 1,
  days: [0, 1, 2, 3, 4, 5, 6],
  hours: { isOpen24Hours: false, open: '07:00', close: '20:00' },
  floors: [{ id: 'f3', name: 'Level 3', floorNumber: 3, spaces: [room] }],
}

// Tomorrow, so every booking below is still ahead (cancellable) whatever time the tests run.
const tomorrow = addDays(nowInZone('UTC').date, 1)

function booking(id: string, title: string, from: string, to: string, day = tomorrow): BookingDto {
  return {
    id,
    spaceId: 's1',
    spaceName: 'Meeting Room 301',
    floorName: 'Level 3',
    buildingName: 'Riverside HQ',
    timezone: 'UTC',
    startsAt: `${day}T${from}:00Z`,
    endsAt: `${day}T${to}:00Z`,
    localStart: `${day}T${from}:00`,
    localEnd: `${day}T${to}:00`,
    attendees: 4,
    title,
    status: 'Confirmed',
    seriesId: null,
    recurrence: null,
    cancelledByAdmin: false,
    cancelReason: null,
    invitees: [],
    isOwner: true,
    ownerName: 'Me',
  }
}

/** What the page gets: the light list for the calendar, and each booking in full once opened. */
function serve(list: BookingDto[]) {
  vi.mocked(getMyBookings).mockResolvedValue(list.map((b) => ({ ...b, isInvited: false })))
  vi.mocked(getBooking).mockImplementation(async (_token, id) => list.find((b) => b.id === id)!)
}

/** The request a month grid makes: its first Sunday to the Saturday after its last day. */
function gridRange(month: string): [string, string, string] {
  const { start, weeks } = monthGrid(month)
  return ['t', start, addDays(start, weeks * 7)]
}

const callsFor = (month: string) =>
  vi.mocked(getMyBookings).mock.calls.filter((c) => c[1] === gridRange(month)[1] && c[2] === gridRange(month)[2]).length

const EMPLOYEE = granted(Permissions.Bookings.Default, Permissions.Bookings.Create, Permissions.Bookings.Cancel)

function renderPage(url = `/my-calendar?view=week&date=${tomorrow}`, permissions: PermissionsValue = EMPLOYEE) {
  render(
    <TestProviders>
      <WithPermissions value={permissions}>
        <MemoryRouter initialEntries={[url]}>
          <MyCalendarPage />
        </MemoryRouter>
      </WithPermissions>
    </TestProviders>,
  )
}

describe('MyCalendarPage', () => {
  beforeEach(() => {
    localStorage.clear()
    vi.mocked(useAuth).mockReturnValue({ user: { access_token: 't' } } as unknown as ReturnType<typeof useAuth>)
    vi.mocked(getMyBookableBuilding).mockResolvedValue(building)
    vi.mocked(getMyBookings).mockReset()
    vi.mocked(getBooking).mockReset()
    serve([booking('b1', 'Design review', '10:00', '11:00')])
    vi.mocked(cancelBooking).mockReset()
  })

  it('loads the month grid around the week once, and shows each booking on it', async () => {
    renderPage()

    expect(await screen.findByRole('button', { name: /Design review, 10:00–11:00, Meeting Room 301/ })).toBeInTheDocument()
    const month = gridMonthFor('week', tomorrow)
    // The week and the mini calendar share one request for the month.
    expect(callsFor(month)).toBe(1)
    expect(screen.getByText('Riverside HQ')).toBeInTheDocument()
  })

  it("prefetches the months either side, so stepping over doesn't wait on the network", async () => {
    const user = userEvent.setup()
    renderPage(`/my-calendar?view=month&date=${tomorrow}`)
    await screen.findByRole('button', { name: /Design review/ })

    const month = gridMonthFor('month', tomorrow)
    await waitFor(() => expect(callsFor(addMonths(month, 1))).toBe(1))
    expect(callsFor(addMonths(month, -1))).toBe(1)

    await user.click(screen.getByRole('button', { name: 'Next' }))
    await user.click(screen.getByRole('button', { name: 'Previous' }))

    // Back on the first month: served from memory, not fetched again.
    expect(callsFor(month)).toBe(1)
  })

  it('moves a week at a time with the arrows', async () => {
    const user = userEvent.setup()
    renderPage()
    await screen.findByRole('button', { name: /Design review/ })

    await user.click(screen.getByRole('button', { name: 'Next' }))

    const next = addDays(startOfWeek(tomorrow), 7)
    const label = screen.getByRole('heading', { level: 2 })
    await waitFor(() => expect(label.textContent).toContain(String(Number(next.slice(8)))))
    await waitFor(() => expect(callsFor(gridMonthFor('week', next))).toBeGreaterThanOrEqual(1))
  })

  it('falls back to today for a date in the URL that does not exist', async () => {
    renderPage('/my-calendar?view=week&date=2026-13-45')

    expect(await screen.findByRole('button', { name: /Design review/ })).toBeInTheDocument()
  })

  it('cancels a booking with a reason and says the room is free again', async () => {
    const user = userEvent.setup()
    const b = booking('b1', 'Design review', '10:00', '11:00')
    vi.mocked(cancelBooking).mockResolvedValue([{ ...b, status: 'Cancelled' }])
    renderPage()

    await user.click(await screen.findByRole('button', { name: /Design review, 10:00–11:00/ }))
    const detail = screen.getByRole('dialog')
    expect(await within(detail).findByText('Upcoming')).toBeInTheDocument()
    await user.click(await within(detail).findByRole('button', { name: 'Cancel booking' }))

    const confirm = screen.getByRole('alertdialog')
    await user.type(within(confirm).getByLabelText(/Reason/), 'Moved online')
    await user.click(within(confirm).getByRole('button', { name: 'Cancel booking' }))

    expect(cancelBooking).toHaveBeenCalledWith('t', 'b1', 'Moved online', 0)
    expect(await screen.findByText('Cancelled — Meeting Room 301 is free again for 10:00–11:00')).toBeInTheDocument()
    // Everything cached is dropped and what's on screen is fetched again.
    await waitFor(() => expect(callsFor(gridMonthFor('week', tomorrow))).toBe(2))
  })

  it('keeps the cancel dialog open with the reason when the server refuses', async () => {
    const user = userEvent.setup()
    const { ApiError } = await import('@/features/bookings/api/bookingsApi')
    vi.mocked(cancelBooking).mockRejectedValue(new ApiError(400, { error: { message: "This booking has already started, so it can't be cancelled." } }))
    renderPage()

    await user.click(await screen.findByRole('button', { name: /Design review/ }))
    await user.click(await within(screen.getByRole('dialog')).findByRole('button', { name: 'Cancel booking' }))
    await user.click(within(screen.getByRole('alertdialog')).getByRole('button', { name: 'Cancel booking' }))

    expect(await within(screen.getByRole('alertdialog')).findByRole('alert')).toHaveTextContent('already started')
  })

  it('folds a busy day into "+N more" in the month view and opens that day', async () => {
    const user = userEvent.setup()
    serve([
      booking('1', 'One', '08:00', '09:00'),
      booking('2', 'Two', '09:00', '10:00'),
      booking('3', 'Three', '10:00', '11:00'),
      booking('4', 'Four', '11:00', '12:00'),
      booking('5', 'Five', '12:00', '13:00'),
    ])
    renderPage(`/my-calendar?view=month&date=${tomorrow}`)

    await user.click(await screen.findByRole('button', { name: '2 more…' }))

    expect(await screen.findByRole('button', { name: /Five, 12:00–13:00/ })).toBeInTheDocument()
    // Day view: the weekday, then "Month D, YYYY".
    const heading = screen.getByRole('heading', { level: 2 }).textContent ?? ''
    expect(heading).toMatch(/^\w+day/)
    expect(heading).toContain(` ${Number(tomorrow.slice(8))}, `)
  })

  it('offers the free rooms for a new booking and opens the form for the one picked', async () => {
    const user = userEvent.setup()
    vi.mocked(searchAvailability).mockResolvedValue({
      buildingId: 'b1',
      buildingName: 'Riverside HQ',
      timezone: 'UTC',
      localStart: '',
      localEnd: '',
      warnings: [],
      spaces: [
        {
          space: room,
          floorId: 'f3',
          floorName: 'Level 3',
          isAvailable: true,
          violations: [],
          freeUntil: '14:00',
          nextFreeStart: null,
          open: [],
          closed: [],
          busy: [],
        },
      ],
    })
    renderPage()

    await user.click(await screen.findByRole('button', { name: 'New booking' }))
    const quick = screen.getByRole('dialog', { name: 'Book a room' })
    await user.click(await within(quick).findByRole('button', { name: /Meeting Room 301/ }))

    expect(await screen.findByRole('dialog', { name: 'Book Meeting Room 301' })).toBeInTheDocument()
  })

  it('calls out rooms ruled out only by length and shortens the time to fit them', async () => {
    const user = userEvent.setup()
    localStorage.setItem('dixels.bookings.lastDurationMinutes', '90') // New booking suggests 1h30
    const pod: BookableSpaceDto = { ...room, id: 's2', name: 'Focus Pod 3-02', maxDurationMinutes: { value: 60, source: 'Space' } }
    const result = (isAvailable: boolean, space: BookableSpaceDto, codes: string[]) => ({
      space,
      floorId: 'f3',
      floorName: 'Level 3',
      isAvailable,
      violations: codes.map((code) => ({ code, level: 'Space', message: code, shortMessage: code })),
      freeUntil: null,
      nextFreeStart: null,
      open: [],
      closed: [],
      busy: [],
    })
    vi.mocked(searchAvailability).mockReset()
    vi.mocked(searchAvailability).mockResolvedValue({
      buildingId: 'b1',
      buildingName: 'Riverside HQ',
      timezone: 'UTC',
      localStart: '',
      localEnd: '',
      warnings: [],
      spaces: [
        result(false, pod, ['Dixels:Bookings:TooLong']),
        result(false, room, ['Dixels:Bookings:Overlap']),
      ],
    })
    renderPage()

    await user.click(await screen.findByRole('button', { name: 'New booking' }))
    const quick = screen.getByRole('dialog', { name: 'Book a room' })

    // The pod would fit if shorter; Meeting Room 301 is booked, so it's grouped as booked.
    expect(await within(quick).findByText('They allow up to 1h — this is 1h 30m.')).toBeInTheDocument()
    expect(within(quick).getByText('Shorter time limit')).toBeInTheDocument()
    expect(within(quick).getByText('Already booked')).toBeInTheDocument()

    const before = vi.mocked(searchAvailability).mock.lastCall![1]
    await user.click(within(quick).getByRole('button', { name: 'Shorten to 1h' }))

    await waitFor(() => {
      const after = vi.mocked(searchAvailability).mock.lastCall![1]
      expect(after.localStart).toBe(before.localStart)
      expect(new Date(after.localEnd).getTime() - new Date(after.localStart).getTime()).toBe(60 * 60 * 1000)
    })
  })

  it('warns in Book a room when I already have another booking then', async () => {
    const user = userEvent.setup()
    vi.mocked(searchAvailability).mockReset()
    vi.mocked(searchAvailability).mockResolvedValue({
      buildingId: 'b1',
      buildingName: 'Riverside HQ',
      timezone: 'UTC',
      localStart: '',
      localEnd: '',
      warnings: [
        {
          code: 'Dixels:Bookings:OwnOverlapWarning',
          level: 'Building',
          message: 'Heads-up: you already have Desk 7 booked Wed 30 Sep 10:00–12:00.',
          shortMessage: "You're in Desk 7 then",
        },
      ],
      spaces: [],
    })
    renderPage()

    await user.click(await screen.findByRole('button', { name: 'New booking' }))

    expect(
      await within(screen.getByRole('dialog', { name: 'Book a room' })).findByText(
        'Heads-up: you already have Desk 7 booked Wed 30 Sep 10:00–12:00.',
      ),
    ).toBeInTheDocument()
  })

  it('marks a series booking, says how it repeats, and cancels this and the following ones', async () => {
    const user = userEvent.setup()
    const series: BookingDto = {
      ...booking('b1', 'Stand-up', '09:00', '09:15'),
      seriesId: 's1',
      recurrence: { frequency: 1, interval: 1, weekdays: [0, 1, 2, 3, 4], monthlyRepeat: 0, endDate: '2026-12-31' },
    }
    serve([series])
    vi.mocked(cancelBooking).mockResolvedValue([series, { ...series, id: 'b2' }, { ...series, id: 'b3' }])
    renderPage()

    const block = await screen.findByRole('button', { name: /Stand-up, 09:00–09:15/ })
    expect(within(block).getByLabelText('Repeats')).toBeInTheDocument()
    await user.click(block)

    const detail = screen.getByRole('dialog')
    expect(await within(detail).findByText(/^Occurs every Sun–Thu until Thu 31 Dec/)).toBeInTheDocument()
    await user.click(await within(detail).findByRole('button', { name: 'Cancel booking' }))

    const confirm = screen.getByRole('alertdialog', { name: 'Cancel recurring booking?' })
    expect(within(confirm).getByLabelText('This event')).toBeChecked()
    await user.click(within(confirm).getByLabelText('This and all following events'))
    await user.click(within(confirm).getByRole('button', { name: 'Cancel booking' }))

    expect(cancelBooking).toHaveBeenCalledWith('t', 'b1', '', 1)
    expect(await screen.findByText('Cancelled 3 bookings of “Stand-up” — Meeting Room 301 is free again then')).toBeInTheDocument()
  })

  it('shows a booking an admin cancelled, struck through, with why', async () => {
    const user = userEvent.setup()
    serve([
      { ...booking('b1', 'Design review', '10:00', '11:00'), status: 'Cancelled', cancelledByAdmin: true, cancelReason: 'Rules changed: Open 09:00–10:00 only' },
    ])
    renderPage()

    await user.click(await screen.findByRole('button', { name: /^Cancelled: Design review, 10:00–11:00/ }))

    const detail = screen.getByRole('dialog')
    expect(await within(detail).findByText('Cancelled by admin')).toBeInTheDocument()
    expect(within(detail).getByRole('status')).toHaveTextContent('An administrator cancelled this booking — Rules changed: Open 09:00–10:00 only.')
    expect(within(detail).queryByRole('button', { name: 'Cancel booking' })).not.toBeInTheDocument()
  })

  it('says so when the employee has no building', async () => {
    vi.mocked(getMyBookableBuilding).mockResolvedValue(null)
    renderPage()

    expect(await screen.findByText(/haven't been assigned to a building/)).toBeInTheDocument()
  })

  it('still shows the calendar, read-only, when the building was removed', async () => {
    vi.mocked(getMyBookableBuilding).mockResolvedValue({ ...building, isRemoved: true, floors: [] })
    serve([
      { ...booking('b1', 'Design review', '10:00', '11:00'), status: 'Cancelled', cancelledByAdmin: true, cancelReason: 'The building was removed' },
    ])
    renderPage()

    expect(await screen.findByText('Riverside HQ is no longer available.')).toBeInTheDocument()
    expect(await screen.findByRole('button', { name: /^Cancelled: Design review/ })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'New booking' })).not.toBeInTheDocument()
  })

  it('opens a booking without offering Cancel to someone without Bookings.Cancel', async () => {
    const user = userEvent.setup()
    renderPage(undefined, granted(Permissions.Bookings.Default, Permissions.Bookings.Create))

    await user.click(await screen.findByRole('button', { name: /Design review, 10:00–11:00/ }))
    const detail = screen.getByRole('dialog')
    expect(await within(detail).findByText('Upcoming')).toBeInTheDocument()
    expect(within(detail).queryByRole('button', { name: 'Cancel booking' })).not.toBeInTheDocument()
  })

  it("shows bookings but offers no way to book to someone without Bookings.Create", async () => {
    renderPage(undefined, granted(Permissions.Bookings.Default, Permissions.Bookings.Cancel))

    expect(await screen.findByRole('button', { name: /Design review, 10:00–11:00, Meeting Room 301/ })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'New booking' })).not.toBeInTheDocument()
  })
})

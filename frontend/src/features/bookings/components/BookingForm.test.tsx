// @vitest-environment jsdom
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ApiError, createBooking, getSpaceDays, previewBooking } from '@/features/bookings/api/bookingsApi'
import type { BookableBuildingDto, BookableSpaceDto, BookingDto, BookingPreviewDto } from '@/features/bookings/api/bookingsApi'
import type { Slot } from '@/features/bookings/suggestSlot'
import { addDays, nowInZone } from '@/lib/time/buildingTime'
import { TestProviders } from '@/test/providers'
import { BookingForm } from './BookingForm'

vi.mock('@/features/bookings/api/bookingsApi', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/features/bookings/api/bookingsApi')>()
  // The room's days stay loading unless a test says otherwise: the plain time limits apply.
  return { ...actual, previewBooking: vi.fn(), createBooking: vi.fn(), getSpaceDays: vi.fn(() => new Promise(() => {})) }
})

const space: BookableSpaceDto = {
  id: 'space-1',
  name: 'Room 1',
  spaceTypeId: 'type-1',
  spaceTypeName: 'Meeting room',
  iconKey: 0,
  capacity: 8,
  minAttendees: 2,
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

function renderForm(onBooked = vi.fn(), initialSlot?: Slot, room: BookableSpaceDto = space) {
  render(
    <BookingForm token="t" building={building} space={room} floorName="Level 1" onClose={vi.fn()} onBooked={onBooked} initialSlot={initialSlot} />,
    { wrapper: TestProviders },
  )
  return onBooked
}

const range = (start: number, end: number) => ({ startMinute: start * 60, endMinute: end * 60, isMine: false })

describe('BookingForm', () => {
  beforeEach(() => {
    vi.mocked(previewBooking).mockReset()
    vi.mocked(createBooking).mockReset()
  })

  it("moves a taken time to the room's next free one and lists only free times", async () => {
    // Radix Select calls pointer-capture and scrollIntoView, which jsdom doesn't implement.
    Element.prototype.hasPointerCapture ??= () => false
    Element.prototype.releasePointerCapture ??= () => {}
    Element.prototype.scrollIntoView ??= () => {}

    const tomorrow = addDays(nowInZone('UTC').date, 1)
    vi.mocked(previewBooking).mockResolvedValue(valid)
    vi.mocked(getSpaceDays).mockResolvedValueOnce({
      days: [{ date: tomorrow, open: [range(8, 18)], closed: [range(12, 13)], busy: [range(10, 11)] }],
    })
    const user = userEvent.setup()

    renderForm(vi.fn(), { date: tomorrow, start: '10:00', end: '11:00' })

    // 10:00–11:00 is booked: the form starts at 11:00 instead, for the same hour.
    const from = screen.getByLabelText('From')
    await waitFor(() => expect(from).toHaveTextContent('11:00'))
    expect(screen.getByLabelText('To')).toHaveTextContent('12:00')

    await user.click(from)
    const offered = screen.getAllByRole('option').map((o) => o.textContent)
    expect(offered[0]).toBe('08:00')
    expect(offered.at(-1)).toBe('17:45')
    expect(offered).not.toContain('10:00') // booked
    expect(offered).not.toContain('12:30') // closed
    expect(offered).not.toContain('18:00') // after closing
  })

  it('moves To along when From changes, keeping the length', async () => {
    Element.prototype.hasPointerCapture ??= () => false
    Element.prototype.releasePointerCapture ??= () => {}
    Element.prototype.scrollIntoView ??= () => {}

    const tomorrow = addDays(nowInZone('UTC').date, 1)
    vi.mocked(previewBooking).mockResolvedValue(valid)
    vi.mocked(getSpaceDays).mockResolvedValueOnce({
      days: [{ date: tomorrow, open: [range(8, 18)], closed: [], busy: [range(10, 11)] }],
    })
    const user = userEvent.setup()

    renderForm(vi.fn(), { date: tomorrow, start: '08:00', end: '09:00' })
    const from = screen.getByLabelText('From')
    const to = screen.getByLabelText('To')

    // Once the room's day is in, its booked hour is gone from the list.
    await user.click(from)
    await waitFor(() => expect(screen.queryByRole('option', { name: '10:00' })).toBeNull())
    await user.click(screen.getByRole('option', { name: '14:00' }))

    // To keeps the hour: 15:00, shown in the box.
    await waitFor(() => expect(to).toHaveTextContent('15:00'))
    expect(from).toHaveTextContent('14:00')
  }, 20_000)

  it('lists every violation from the server, the most fundamental first, and blocks booking', async () => {
    vi.mocked(previewBooking).mockResolvedValue({
      ...valid,
      isValid: false,
      violations: [
        { code: 'Dixels:Bookings:BelowMinAttendees', level: 'Space', message: 'This space needs at least 2 attendees (Space rule).', shortMessage: 'Needs at least 2 people' },
        { code: 'Dixels:Bookings:TooLong', level: 'Building', message: 'Bookings here can last at most 2h (Building rule).', shortMessage: 'Max 2h per booking' },
      ],
    })

    renderForm()

    // Each rule sits under the field it's about, not in one panel at the bottom. The room's
    // minimum is said as what to do: invite one more (the booker counts as one).
    const people = screen.getByRole('group', { name: 'People' })
    expect(people).toHaveTextContent('1 person · just you')
    expect(people).toHaveTextContent('This room needs at least 2 people: invite 1 more.')
    const from = screen.getByLabelText('From')
    await waitFor(() => expect(from).toHaveAttribute('aria-invalid', 'true'))
    expect(from).toHaveAccessibleDescription(/at most 2h/)
    expect(from).toHaveAttribute('aria-invalid', 'true')
    expect(screen.getByRole('button', { name: 'Book' })).toBeDisabled()
  })

  it('books a free slot with the typed title and a stable idempotency key', async () => {
    vi.mocked(previewBooking).mockResolvedValue(valid)
    const booking = { id: 'bk-1' } as BookingDto
    vi.mocked(createBooking).mockResolvedValue(booking)
    const user = userEvent.setup()
    const onBooked = renderForm()

    await user.type(screen.getByLabelText('Title'), 'Planning')
    await screen.findByText('Available')
    await user.click(screen.getByRole('button', { name: 'Book' }))

    await waitFor(() => expect(onBooked).toHaveBeenCalledWith(booking))
    const sent = vi.mocked(createBooking).mock.calls[0][1]
    expect(sent).toMatchObject({ spaceId: 'space-1', title: 'Planning' })
    // The head count isn't sent: the server counts the booker and the guests.
    expect(sent).not.toHaveProperty('attendees')
    expect(sent.idempotencyKey).toMatch(/^[0-9a-f-]{36}$/)
  })

  it('shows the head count read-only, with nothing to type, and too many people under it', async () => {
    vi.mocked(previewBooking).mockResolvedValue({
      ...valid,
      isValid: false,
      violations: [
        {
          code: 'Dixels:Bookings:OverCapacity',
          level: 'Space',
          message: "This space seats 8, but with your guests you're 9 people. Invite fewer people or pick a larger space.",
          shortMessage: 'Seats 8 — you need 9',
        },
      ],
    })
    renderForm(vi.fn(), undefined, { ...space, minAttendees: null })

    const people = screen.getByRole('group', { name: 'People' })
    expect(screen.queryByRole('spinbutton')).not.toBeInTheDocument()
    expect(await within(people).findByText(/you're 9 people/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Book' })).toBeDisabled()
  })

  it('does not re-check availability while only the title changes', async () => {
    vi.mocked(previewBooking).mockResolvedValue(valid)
    const user = userEvent.setup()
    renderForm()
    await screen.findByText('Available')

    await user.type(screen.getByLabelText('Title'), 'Planning')
    await screen.findByText('Available')

    expect(previewBooking).toHaveBeenCalledTimes(1)
  })

  it('re-checks after losing a race for the slot', async () => {
    vi.mocked(previewBooking).mockResolvedValue(valid)
    vi.mocked(createBooking).mockRejectedValue(
      new ApiError(409, { error: { code: 'Dixels:Bookings:Overlap', message: 'This space is already booked for part of that time.' } }),
    )
    const user = userEvent.setup()
    renderForm()

    await screen.findByText('Available')
    await user.click(screen.getByRole('button', { name: 'Book' }))

    expect(await screen.findByText('This space is already booked for part of that time.')).toBeInTheDocument()
    await waitFor(() => expect(previewBooking).toHaveBeenCalledTimes(2))
  })

  it("re-checks when the room's rules changed since the preview", async () => {
    vi.mocked(previewBooking).mockResolvedValue(valid)
    // A rule rejection is a plain BusinessException — ABP's default 403, not the 409 above.
    vi.mocked(createBooking).mockRejectedValue(
      new ApiError(403, { error: { code: 'Dixels:Bookings:Rejected', message: 'This space is closed at that time.' } }),
    )
    const user = userEvent.setup()
    renderForm()

    await screen.findByText('Available')
    await user.click(screen.getByRole('button', { name: 'Book' }))

    expect(await screen.findByText('This space is closed at that time.')).toBeInTheDocument()
    await waitFor(() => expect(previewBooking).toHaveBeenCalledTimes(2))
  })
})

// @vitest-environment jsdom
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ApiError, createBooking, previewBooking } from '@/features/bookings/api/bookingsApi'
import type { BookableBuildingDto, BookableSpaceDto, BookingDto, BookingPreviewDto } from '@/features/bookings/api/bookingsApi'
import { BookingForm } from './BookingForm'

vi.mock('@/features/bookings/api/bookingsApi', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/features/bookings/api/bookingsApi')>()
  return { ...actual, previewBooking: vi.fn(), createBooking: vi.fn() }
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
  minLeadMinutes: 0,
  slotMinutes: 15,
  days: [0, 1, 2, 3, 4, 5, 6],
  hours: { isOpen24Hours: false, open: '07:00', close: '20:00' },
  floors: [{ id: 'f-1', name: 'Level 1', floorNumber: 1, spaces: [space] }],
}

const valid: BookingPreviewDto = { isValid: true, violations: [], startsAt: '', endsAt: '', timezone: 'UTC' }

function renderForm(onBooked = vi.fn()) {
  render(
    <BookingForm token="t" building={building} space={space} floorName="Level 1" onClose={vi.fn()} onBooked={onBooked} />,
  )
  return onBooked
}

describe('BookingForm', () => {
  beforeEach(() => {
    vi.mocked(previewBooking).mockReset()
    vi.mocked(createBooking).mockReset()
  })

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

    // Each rule sits under the field it's about, not in one panel at the bottom.
    const attendees = screen.getByLabelText('Attendees')
    await waitFor(() => expect(attendees).toHaveAccessibleDescription(/at least 2 attendees/))
    expect(attendees).toHaveAttribute('aria-invalid', 'true')
    const from = screen.getByLabelText('From')
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
    expect(sent).toMatchObject({ spaceId: 'space-1', attendees: 2, title: 'Planning' })
    expect(sent.idempotencyKey).toMatch(/^[0-9a-f-]{36}$/)
  })

  it('says under Attendees when there are more people than seats, without asking the server', async () => {
    vi.mocked(previewBooking).mockResolvedValue(valid)
    const user = userEvent.setup()
    renderForm()
    await screen.findByText('Available')

    const attendees = screen.getByLabelText('Attendees')
    await user.clear(attendees)
    await user.type(attendees, '9')

    expect(screen.getByText('This room seats 8.')).toBeInTheDocument()
    expect(attendees).toHaveAttribute('aria-invalid', 'true')
    expect(attendees).toHaveAccessibleDescription('This room seats 8.')
    expect(screen.getByRole('button', { name: 'Book' })).toBeDisabled()
    // Only the opening check ran — an impossible number never goes to the server.
    expect(previewBooking).toHaveBeenCalledTimes(1)
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

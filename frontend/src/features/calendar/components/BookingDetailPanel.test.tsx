// @vitest-environment jsdom
import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import type { BookingDto } from '@/features/bookings/api/bookingsApi'
import type { CalendarItem } from '@/features/calendar/calendarItem'
import { BookingDetailPanel } from './BookingDetailPanel'

const item: CalendarItem = {
  id: 'b1',
  kind: 'booking',
  title: 'Product demo',
  localStart: '2027-01-10T13:30:00',
  localEnd: '2027-01-10T15:30:00',
  location: 'Meeting Room 302',
  cancelled: false,
  repeats: false,
}

const booking = {
  id: 'b1',
  spaceId: 's1',
  spaceName: 'Meeting Room 302',
  floorName: 'Level 3',
  buildingName: 'Riverside HQ',
  timezone: 'UTC',
  startsAt: '2027-01-10T13:30:00Z',
  endsAt: '2027-01-10T15:30:00Z',
  localStart: '2027-01-10T13:30:00',
  localEnd: '2027-01-10T15:30:00',
  attendees: 4,
  title: 'Product demo',
  status: 'Confirmed',
} as BookingDto

function renderPanel(full: BookingDto | null, onCancel = vi.fn(), onClose = vi.fn(), canBook = true, canCancel = true) {
  render(
    <MemoryRouter>
      <BookingDetailPanel item={item} booking={full} error={null} canBook={canBook} canCancel={canCancel} onClose={onClose} onCancel={onCancel} />
    </MemoryRouter>,
  )
  return { onCancel, onClose }
}

describe('BookingDetailPanel', () => {
  it('shows the day, the time on a 12-hour clock and the room, then the rest once loaded', () => {
    renderPanel(null)
    const panel = screen.getByRole('region', { name: 'Booking details' })
    expect(panel).toHaveAttribute('aria-busy', 'true')
    expect(panel).toHaveTextContent('Sunday 10 January 2027')
    expect(panel).toHaveTextContent('13:30 – 15:30')
    expect(panel).toHaveTextContent('Meeting Room 302')
    expect(screen.queryByRole('button', { name: 'Cancel booking' })).not.toBeInTheDocument()
  })

  it('offers Cancel for an upcoming booking and closes on the X', async () => {
    const user = userEvent.setup()
    const { onCancel, onClose } = renderPanel(booking)

    expect(screen.getByText('Upcoming')).toBeInTheDocument()
    expect(screen.getByText('4 people')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Other rooms at this time/ })).toHaveAttribute(
      'href',
      '/find-space?date=2027-01-10&from=13:30&to=15:30&people=4',
    )

    await user.click(screen.getByRole('button', { name: 'Cancel booking' }))
    expect(onCancel).toHaveBeenCalledWith(booking)
    await user.click(screen.getByRole('button', { name: 'Close details' }))
    expect(onClose).toHaveBeenCalled()
  })

  it("doesn't point someone who can't book at other rooms", () => {
    renderPanel(booking, vi.fn(), vi.fn(), false)

    expect(screen.getByRole('button', { name: 'Cancel booking' })).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /Other rooms at this time/ })).not.toBeInTheDocument()
  })

  it("doesn't offer Cancel to someone without Bookings.Cancel", () => {
    renderPanel(booking, vi.fn(), vi.fn(), true, false)

    expect(screen.getByText('Upcoming')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Cancel booking' })).not.toBeInTheDocument()
  })
})

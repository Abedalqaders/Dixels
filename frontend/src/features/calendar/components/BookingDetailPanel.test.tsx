// @vitest-environment jsdom
import { describe, expect, it, vi } from 'vitest'
import { render, screen, within } from '@testing-library/react'
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
  invited: false,
  declined: false,
  maybe: false,
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
  isOwner: true,
  ownerName: 'Sara Ali',
  invitees: [],
  myResponse: null,
} as unknown as BookingDto

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
    expect(panel).toHaveTextContent('13:30–15:30')
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

  it("lists the owner's guests with their emails, outside ones tagged Guest", () => {
    const withGuests: BookingDto = {
      ...booking,
      isOwner: true,
      invitees: [
        { userId: 'u-sara', name: 'Sara Ali', email: 'sara@dixels.io', isExternal: false, responseStatus: 0, isBusy: false, busyDates: 0, maybeBusyDates: 0, busyTimes: [] },
        { userId: null, name: 'Omar Farouk', email: 'omar@acme.com', isExternal: true, responseStatus: 0, isBusy: false, busyDates: 0, maybeBusyDates: 0, busyTimes: [] },
      ],
    }
    renderPanel(withGuests)

    const panel = screen.getByRole('region', { name: 'Booking details' })
    expect(panel).toHaveTextContent('2 invited')
    const list = within(panel).getByRole('list', { name: 'Invited' })
    const [sara, omar] = within(list).getAllByRole('listitem')
    expect(sara).toHaveTextContent('Sara Ali')
    expect(sara).toHaveTextContent('sara@dixels.io')
    expect(sara).not.toHaveTextContent('Guest')
    expect(omar).toHaveTextContent('Omar Farouk')
    expect(omar).toHaveTextContent('omar@acme.com')
    expect(omar).toHaveTextContent('Guest')
  })

  it('shows no guest list when nobody was invited', () => {
    renderPanel(booking)
    expect(screen.queryByRole('list', { name: 'Invited' })).not.toBeInTheDocument()
  })

  it('offers Edit guests on an upcoming booking I own, and only then', async () => {
    const onEditGuests = vi.fn()
    const mine = { ...booking, isOwner: true } as BookingDto
    const { rerender } = render(
      <MemoryRouter>
        <BookingDetailPanel item={item} booking={mine} error={null} canBook canCancel onClose={vi.fn()} onCancel={vi.fn()} onEditGuests={onEditGuests} />
      </MemoryRouter>,
    )

    await userEvent.setup().click(screen.getByRole('button', { name: 'Edit guests' }))
    expect(onEditGuests).toHaveBeenCalledWith(mine)

    rerender(
      <MemoryRouter>
        <BookingDetailPanel item={item} booking={{ ...mine, isOwner: false }} error={null} canBook canCancel onClose={vi.fn()} onCancel={vi.fn()} onEditGuests={onEditGuests} />
      </MemoryRouter>,
    )
    expect(screen.queryByRole('button', { name: 'Edit guests' })).not.toBeInTheDocument()
  })

  it("shows a guest who invited them, the other guests by name only, and no Cancel", () => {
    const invite = {
      ...booking,
      isOwner: false,
      invitees: [
        { userId: 'u-me', name: 'Jordan Reed', email: '', isExternal: false, responseStatus: 0, isBusy: false, busyDates: 0, maybeBusyDates: 0, busyTimes: [] },
        { userId: null, name: 'Omar Farouk', email: '', isExternal: true, responseStatus: 0, isBusy: false, busyDates: 0, maybeBusyDates: 0, busyTimes: [] },
      ],
    } as BookingDto
    renderPanel(invite)

    const panel = screen.getByRole('region', { name: 'Booking details' })
    expect(panel).toHaveTextContent('Invited by Sara Ali')
    const list = within(panel).getByRole('list', { name: 'Invited' })
    expect(list).toHaveTextContent('Jordan Reed')
    expect(list).not.toHaveTextContent('@')
    expect(screen.queryByRole('button', { name: 'Cancel booking' })).not.toBeInTheDocument()
  })

  it('a guest sees an admin-cancelled invite struck through, with the reason', () => {
    renderPanel({ ...booking, isOwner: false, status: 'Cancelled', cancelledByAdmin: true, cancelReason: 'The room is closed for maintenance.' } as BookingDto)

    const panel = screen.getByRole('region', { name: 'Booking details' })
    expect(panel).toHaveTextContent('Invited by Sara Ali')
    expect(panel).toHaveTextContent(
      "The organiser's booking was cancelled by an administrator — The room is closed for maintenance.. This meeting won't take place here.",
    )
    // Not the owner's wording: the room was never held for a guest.
    expect(panel).not.toHaveTextContent('held for you')
    expect(screen.queryByRole('button', { name: 'Cancel booking' })).not.toBeInTheDocument()
  })

  it("the owner still reads that the room is no longer held for them", () => {
    renderPanel({ ...booking, status: 'Cancelled', cancelledByAdmin: true, cancelReason: 'Closed' } as BookingDto)
    expect(screen.getByRole('region', { name: 'Booking details' })).toHaveTextContent('held for you')
  })

  it("everyone invited sees each guest's answer and the tally", () => {
    const invite = {
      ...booking,
      isOwner: false,
      myResponse: 1,
      invitees: [
        { userId: 'u-1', name: 'Jordan Reed', email: '', isExternal: false, responseStatus: 1 },
        { userId: 'u-2', name: 'Leo Tran', email: '', isExternal: false, responseStatus: 2 },
        { userId: null, name: 'Omar Farouk', email: '', isExternal: true, responseStatus: 0 },
      ],
    } as BookingDto
    render(
      <MemoryRouter>
        <BookingDetailPanel item={item} booking={invite} error={null} canBook canCancel onClose={vi.fn()} onCancel={vi.fn()} onRespond={vi.fn()} />
      </MemoryRouter>,
    )

    const panel = screen.getByRole('region', { name: 'Booking details' })
    expect(panel).toHaveTextContent('Accepted 1 · Declined 1 · Waiting 1')
    const [jordan, leo, omar] = within(within(panel).getByRole('list', { name: 'Invited' })).getAllByRole('listitem')
    expect(within(jordan).getByRole('img', { name: 'Accepted' })).toBeInTheDocument()
    expect(within(leo).getByRole('img', { name: 'Declined' })).toBeInTheDocument()
    expect(within(omar).getByRole('img', { name: 'No answer yet' })).toBeInTheDocument()
    // A guest gets the answer buttons, their own answer already marked.
    expect(within(panel).getByRole('button', { name: 'Accepted' })).toHaveAttribute('aria-pressed', 'true')
  })

  it('the owner gets no answer buttons', () => {
    render(
      <MemoryRouter>
        <BookingDetailPanel item={item} booking={booking} error={null} canBook canCancel onClose={vi.fn()} onCancel={vi.fn()} onRespond={vi.fn()} />
      </MemoryRouter>,
    )
    expect(screen.queryByRole('button', { name: 'Accept' })).not.toBeInTheDocument()
  })
})

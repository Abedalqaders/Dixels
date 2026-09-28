// @vitest-environment jsdom
import { describe, expect, it, vi } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { BookingImpactDto } from '@/features/space-management/api/spaceManagementApi'
import { BookingImpactDialog } from './BookingImpactDialog'

const impact: BookingImpactDto = {
  count: 2,
  bookings: [
    {
      bookingId: 'b1',
      title: 'Planning',
      bookedBy: 'Jordan Reed',
      spaceName: 'Room 1',
      floorName: 'Level 2',
      localStart: '2026-09-30T17:00:00',
      localEnd: '2026-09-30T18:00:00',
      reasons: ['Open 09:00–17:00 only'],
    },
    {
      bookingId: 'b2',
      title: 'Retro',
      bookedBy: 'Amira Hassan',
      spaceName: 'Room 2',
      floorName: 'Level 2',
      localStart: '2026-10-01T08:00:00',
      localEnd: '2026-10-01T09:00:00',
      reasons: ['Open 09:00–17:00 only'],
    },
  ],
}

describe('BookingImpactDialog', () => {
  it('lists who, when, where and why, and lets the admin keep or cancel them', async () => {
    const user = userEvent.setup()
    const onChoose = vi.fn()
    render(<BookingImpactDialog mode="change" impact={impact} onChoose={onChoose} />)

    const dialog = screen.getByRole('alertdialog', { name: 'This change affects 2 upcoming bookings' })
    const rows = within(dialog).getAllByRole('listitem')
    expect(rows[0]).toHaveTextContent('Wed 30 Sep · 17:00–18:00')
    expect(rows[0]).toHaveTextContent('Room 1 · Level 2')
    expect(rows[0]).toHaveTextContent('Jordan Reed · Planning')
    expect(rows[0]).toHaveTextContent('Open 09:00–17:00 only')

    await user.click(within(dialog).getByRole('button', { name: 'Keep them and save' }))
    expect(onChoose).toHaveBeenLastCalledWith('keep')
  })

  it('offers to cancel them instead', async () => {
    const user = userEvent.setup()
    const onChoose = vi.fn()
    render(<BookingImpactDialog mode="closure" impact={impact} onChoose={onChoose} />)

    expect(screen.getByRole('alertdialog', { name: 'This closure falls on 2 upcoming bookings' })).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Cancel 2 and add the closure' }))
    expect(onChoose).toHaveBeenLastCalledWith('cancel')
  })

  it('for a delete, only confirms — the bookings go with the room', async () => {
    const user = userEvent.setup()
    const onChoose = vi.fn()
    render(<BookingImpactDialog mode="delete" subject="Level 2" impact={impact} onChoose={onChoose} />)

    expect(screen.getByRole('alertdialog', { name: 'Deleting “Level 2” cancels 2 upcoming bookings' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Keep them/ })).not.toBeInTheDocument()
    expect(screen.getByText(/Restoring later won't bring them back/)).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Go back' }))
    expect(onChoose).toHaveBeenLastCalledWith(null)
  })

  it('warns that assigned employees will be left without a building, even with no bookings', async () => {
    const user = userEvent.setup()
    const onChoose = vi.fn()
    render(<BookingImpactDialog mode="delete" subject="Riverside HQ" impact={{ count: 0, bookings: [], assignedEmployees: 8 }} onChoose={onChoose} />)

    expect(screen.getByRole('alertdialog', { name: 'Delete “Riverside HQ”?' })).toBeInTheDocument()
    expect(screen.getByRole('note')).toHaveTextContent("8 employees are assigned to Riverside HQ. They won't be able to book")
    expect(screen.queryByRole('list', { name: 'Affected bookings' })).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Delete' }))
    expect(onChoose).toHaveBeenLastCalledWith('cancel')
  })

  it('when moving someone, asks whether to keep their bookings in the old building', async () => {
    const user = userEvent.setup()
    const onChoose = vi.fn()
    render(<BookingImpactDialog mode="reassign" subject="Jordan Reed" impact={impact} onChoose={onChoose} />)

    expect(screen.getByRole('alertdialog', { name: 'Jordan Reed has 2 upcoming bookings in their current building' })).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Keep them and move' }))
    expect(onChoose).toHaveBeenLastCalledWith('keep')
  })
})

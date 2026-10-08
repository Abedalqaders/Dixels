// @vitest-environment jsdom
import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { CalendarItem } from '@/features/calendar/calendarItem'
import { MonthGrid } from './MonthGrid'

const item = (id: string, hour: string, cancelled = false, invited = false, declined = false): CalendarItem => ({
  id,
  kind: 'booking',
  title: id,
  localStart: `2026-10-06T${hour}:00:00`,
  localEnd: `2026-10-06T${hour}:30:00`,
  location: 'Room',
  cancelled,
  repeats: false,
  invited,
  declined,
  maybe: false,
})
const four = [item('One', '09'), item('Two', '10'), item('Three', '11'), item('Four', '12')]

describe('MonthGrid', () => {
  it('shows each chip as title and start time, and folds the rest into "N more…"', () => {
    render(<MonthGrid date="2026-10-01" items={four} today="2026-10-01" onOpenItem={vi.fn()} onOpenDay={vi.fn()} />)

    expect(screen.getByRole('button', { name: /One, 09:00–09:30, Room/ })).toHaveTextContent('One09:00')
    expect(screen.queryByRole('button', { name: /Four, / })).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: '1 more…' })).toBeInTheDocument()
  })

  it('compact: a dot per booking, "+N" for the rest, and the whole day opens on tap', async () => {
    const user = userEvent.setup()
    const onOpenDay = vi.fn()
    render(<MonthGrid compact date="2026-10-01" items={four} today="2026-10-01" onOpenItem={vi.fn()} onOpenDay={onOpenDay} />)

    const day = screen.getByRole('button', { name: 'Open Tue 6 Oct, 4 bookings' })
    expect(day).toHaveTextContent('+1')
    expect(screen.queryByRole('button', { name: /One, / })).not.toBeInTheDocument()

    await user.click(day)
    expect(onOpenDay).toHaveBeenCalledWith('2026-10-06')
  })
})

describe('MonthGrid invites', () => {
  it("draws a booking I'm invited to as a dashed outline and says so", () => {
    render(<MonthGrid date="2026-10-01" items={[item('Mine', '09'), item('Sync', '10', false, true)]} today="2026-10-01" onOpenItem={vi.fn()} onOpenDay={vi.fn()} />)

    const invite = screen.getByRole('button', { name: 'Invite: Sync, 10:00–10:30, Room' })
    expect(invite).toHaveClass('border-dashed', 'border-brand', 'bg-[var(--surface-raised)]')
    expect(screen.getByRole('button', { name: 'Mine, 09:00–09:30, Room' })).not.toHaveClass('border-dashed')
  })

  it('an invite an admin cancelled is struck through like my own cancelled bookings', () => {
    render(<MonthGrid date="2026-10-01" items={[item('Sync', '10', true, true)]} today="2026-10-01" onOpenItem={vi.fn()} onOpenDay={vi.fn()} />)

    const cancelled = screen.getByRole('button', { name: 'Cancelled: Sync, 10:00–10:30, Room' })
    expect(cancelled).toHaveClass('line-through', 'bg-muted')
    expect(cancelled).not.toHaveClass('bg-[var(--surface-raised)]')
  })
})

describe('MonthGrid declined invites', () => {
  it('draws an invite I declined faded and struck through, still as an outline', () => {
    render(<MonthGrid date="2026-10-01" items={[item('Sync', '10', false, true, true)]} today="2026-10-01" onOpenItem={vi.fn()} onOpenDay={vi.fn()} />)

    const declined = screen.getByRole('button', { name: 'Declined: Sync, 10:00–10:30, Room' })
    expect(declined).toHaveClass('border-dashed', 'line-through', 'opacity-60')
  })
})

describe('MonthGrid on touch screens', () => {
  it('shows the "+" to book a day without needing hover, at a finger-sized target', () => {
    render(<MonthGrid date="2026-10-01" items={[]} today="2026-10-01" onOpenItem={vi.fn()} onOpenDay={vi.fn()} onQuickBook={vi.fn()} />)

    const plus = screen.getByRole('button', { name: 'Book a room on Tue 6 Oct' })
    expect(plus).toHaveClass('pointer-coarse:opacity-100', 'pointer-coarse:size-9')
  })
})

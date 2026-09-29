// @vitest-environment jsdom
import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { CalendarItem } from '@/features/calendar/calendarItem'
import { MonthGrid } from './MonthGrid'

const item = (id: string, hour: string, cancelled = false): CalendarItem => ({
  id,
  kind: 'booking',
  title: id,
  localStart: `2026-10-06T${hour}:00:00`,
  localEnd: `2026-10-06T${hour}:30:00`,
  location: 'Room',
  cancelled,
  repeats: false,
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

    const day = screen.getByRole('button', { name: 'Open 2026-10-06, 4 bookings' })
    expect(day).toHaveTextContent('+1')
    expect(screen.queryByRole('button', { name: /One, / })).not.toBeInTheDocument()

    await user.click(day)
    expect(onOpenDay).toHaveBeenCalledWith('2026-10-06')
  })
})

describe('MonthGrid on touch screens', () => {
  it('shows the "+" to book a day without needing hover, at a finger-sized target', () => {
    render(<MonthGrid date="2026-10-01" items={[]} today="2026-10-01" onOpenItem={vi.fn()} onOpenDay={vi.fn()} onQuickBook={vi.fn()} />)

    const plus = screen.getByRole('button', { name: 'Book a room on 2026-10-06' })
    expect(plus).toHaveClass('pointer-coarse:opacity-100', 'pointer-coarse:size-9')
  })
})

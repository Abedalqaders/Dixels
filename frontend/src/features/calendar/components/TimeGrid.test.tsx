// @vitest-environment jsdom
import { beforeAll, describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import { buildDurationLimits } from '@/features/calendar/durationLimits'
import { HOUR_PX, itemsByDay } from '@/features/calendar/dayLayout'
import type { CalendarItem } from '@/features/calendar/calendarItem'
import { TimeGrid } from './TimeGrid'

// jsdom has no layout: every column starts at y = 0, so a minute's y is just its offset.
beforeAll(() => {
  Element.prototype.setPointerCapture ??= () => {}
})

// The grid runs from midnight, so a minute's y is just its offset.
const y = (h: number, m = 0) => ((h * 60 + m) / 60) * HOUR_PX
const WEEK = [0, 1, 2, 3, 4, 5, 6]

// Rooms allow at most 1h or 3h — the drag can't go past 3h anywhere.
const limits = buildDurationLimits({
  floors: [
    {
      id: 'f',
      name: 'L',
      floorNumber: 1,
      spaces: [60, 180].map((value, i) => ({ id: String(i), maxDurationMinutes: { value, source: 'Space' } })) as never,
    },
  ],
})

function renderGrid(day: string, onPickRange = vi.fn(), openDays = WEEK, items: CalendarItem[] = []) {
  const { container } = render(
    <TimeGrid
      days={[day]}
      items={items}
      openDays={openDays}
      openHours={{ isOpen24Hours: false, open: '08:00', close: '20:00' }}
      today="2026-10-01"
      nowMinute={10 * 60}
      firstBookableMinute={10 * 60 + 15}
      lastBookableDate="2026-10-31"
      leadMinutes={15}
      slotMinutes={15}
      defaultLength={60}
      limits={limits}
      onOpenItem={vi.fn()}
      onPickRange={onPickRange}
    />,
  )
  const column = container.querySelector<HTMLElement>('.touch-pan-y')!
  return { column, onPickRange }
}

describe('itemsByDay', () => {
  const b = (id: string, localStart: string, localEnd: string) => ({ id, localStart, localEnd }) as never

  it('puts a booking on each day it covers, but not on the day after one ending at midnight', () => {
    const untilMidnight = b('late', '2026-09-30T22:00:00', '2026-10-01T00:00:00')
    const overnight = b('night', '2026-09-30T22:00:00', '2026-10-01T02:00:00')

    const byDay = itemsByDay([untilMidnight, overnight], ['2026-09-30', '2026-10-01'])

    expect(byDay.get('2026-09-30')).toEqual([untilMidnight, overnight])
    expect(byDay.get('2026-10-01')).toEqual([overnight])
  })
})

describe('TimeGrid', () => {
  it('explains a click on time that is too soon to book', () => {
    const { column, onPickRange } = renderGrid('2026-10-01')

    fireEvent.pointerDown(column, { button: 0, pointerId: 1, clientY: y(10, 5) })

    expect(screen.getByRole('status')).toHaveTextContent(
      'Too soon: bookings need 15 min notice. The earliest you can start today is 10:15.',
    )
    expect(onPickRange).not.toHaveBeenCalled()
  })

  it('explains a click on time that has passed, and on a past day', () => {
    const { column } = renderGrid('2026-10-01')
    fireEvent.pointerDown(column, { button: 0, pointerId: 1, clientY: y(9) })
    expect(screen.getByRole('status')).toHaveTextContent('That time has passed.')
  })

  it('says a whole past day has passed', () => {
    const { column } = renderGrid('2026-09-30')
    fireEvent.pointerDown(column, { button: 0, pointerId: 1, clientY: y(15) })
    expect(screen.getByRole('status')).toHaveTextContent('This day has passed.')
  })

  it('explains a click before the building opens', () => {
    const { column, onPickRange } = renderGrid('2026-10-02')
    fireEvent.pointerDown(column, { button: 0, pointerId: 1, clientY: y(7) })
    expect(screen.getByRole('status')).toHaveTextContent('The building opens at 08:00.')
    expect(onPickRange).not.toHaveBeenCalled()
  })

  it('says the building is closed all day on a day outside its opening days', () => {
    // 2 Oct 2026 is a Friday; the building is open Sun–Thu.
    const { column, onPickRange } = renderGrid('2026-10-02', vi.fn(), [0, 1, 2, 3, 4])
    fireEvent.pointerDown(column, { button: 0, pointerId: 1, clientY: y(11) })
    expect(screen.getByRole('status')).toHaveTextContent('The building is closed on Fridays.')
    expect(onPickRange).not.toHaveBeenCalled()
  })

  it('says a day past the booking window is too far ahead', () => {
    const { column, onPickRange } = renderGrid('2026-11-01')
    fireEvent.pointerDown(column, { button: 0, pointerId: 1, clientY: y(11) })
    expect(screen.getByRole('status')).toHaveTextContent(/Bookable up to .*31.*Oct/)
    expect(onPickRange).not.toHaveBeenCalled()
  })

  it('previews where a click would book before pressing', () => {
    const { column } = renderGrid('2026-10-02')
    fireEvent.pointerMove(column, { pointerId: 1, pointerType: 'mouse', clientY: y(11, 10) })
    expect(screen.getByText('+ 11:00')).toBeInTheDocument()
  })

  it('stops a drag at the longest any room allows and says so', () => {
    const { column, onPickRange } = renderGrid('2026-10-02')

    fireEvent.pointerDown(column, { button: 0, pointerId: 1, clientY: y(9) })
    fireEvent.pointerMove(column, { pointerId: 1, clientY: y(10, 30) })
    expect(screen.queryByText(/max/)).not.toBeInTheDocument()

    fireEvent.pointerMove(column, { pointerId: 1, clientY: y(14) })
    expect(screen.getByText('3h max')).toBeInTheDocument()
    fireEvent.pointerUp(column, { pointerId: 1 })

    expect(onPickRange).toHaveBeenCalledWith({ date: '2026-10-02', start: 9 * 60, end: 12 * 60 })
  })
})

describe('TimeGrid invites', () => {
  it("draws a booking I'm invited to as a dashed outline with a people icon", () => {
    const invite: CalendarItem = {
      id: 'sync',
      kind: 'booking',
      title: 'Sync',
      localStart: '2026-10-02T11:00:00',
      localEnd: '2026-10-02T12:00:00',
      location: 'Room 301',
      cancelled: false,
      repeats: false,
      invited: true,
    }
    renderGrid('2026-10-02', vi.fn(), WEEK, [invite])

    const block = screen.getByRole('button', { name: 'Invite: Sync, 11:00–12:00, Room 301' })
    expect(block).toHaveClass('border-dashed', 'border-brand', 'bg-background')
    expect(block.querySelector('svg.lucide-users')).not.toBeNull()
  })
})

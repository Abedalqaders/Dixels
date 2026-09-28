// @vitest-environment jsdom
import { beforeAll, describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import { buildDurationLimits } from '@/features/calendar/durationLimits'
import { bookingsByDay, HOUR_PX, TimeGrid } from './TimeGrid'

// jsdom has no layout: every column starts at y = 0, so a minute's y is just its offset.
beforeAll(() => {
  Element.prototype.setPointerCapture ??= () => {}
})

const HOURS = { from: 8, to: 20 }
const y = (h: number, m = 0) => ((h * 60 + m - HOURS.from * 60) / 60) * HOUR_PX

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

function renderGrid(day: string, onPickRange = vi.fn()) {
  const { container } = render(
    <TimeGrid
      days={[day]}
      bookings={[]}
      hours={HOURS}
      today="2026-10-01"
      nowMinute={10 * 60}
      firstBookableMinute={10 * 60 + 15}
      leadMinutes={15}
      slotMinutes={15}
      defaultLength={60}
      limits={limits}
      onOpenBooking={vi.fn()}
      onPickRange={onPickRange}
    />,
  )
  const column = container.querySelector<HTMLElement>('.touch-pan-y')!
  return { column, onPickRange }
}

describe('bookingsByDay', () => {
  const b = (id: string, localStart: string, localEnd: string) => ({ id, localStart, localEnd }) as never

  it('puts a booking on each day it covers, but not on the day after one ending at midnight', () => {
    const untilMidnight = b('late', '2026-09-30T22:00:00', '2026-10-01T00:00:00')
    const overnight = b('night', '2026-09-30T22:00:00', '2026-10-01T02:00:00')

    const byDay = bookingsByDay([untilMidnight, overnight], ['2026-09-30', '2026-10-01'])

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

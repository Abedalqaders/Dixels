// @vitest-environment jsdom
import { beforeAll, describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import { DayBar } from './DayBar'
import type { DayBarPick } from './DayBar'

const h = (hours: number, minutes = 0) => hours * 60 + minutes

// The bar spans 07:00–20:00 (780 minutes) over 780px, so 1px = 1 minute: clientX 120 = 09:00.
const AXIS = { from: h(7), to: h(20) }
const x = (minute: number) => minute - AXIS.from

beforeAll(() => {
  // jsdom has no layout or pointer capture.
  HTMLElement.prototype.getBoundingClientRect = function () {
    return { left: 0, top: 0, width: 780, height: 24, right: 780, bottom: 24, x: 0, y: 0, toJSON: () => ({}) }
  }
  HTMLElement.prototype.setPointerCapture = () => {}
})

function renderBar() {
  const onPick = vi.fn()
  const pick: DayBarPick = {
    rules: {
      open: [{ start: h(7), end: h(20) }],
      blockers: [{ start: h(12), end: h(13) }],
      slotMinutes: 15,
      minStart: 0,
      maxDuration: 240,
    },
    defaultLength: 60,
    roomName: 'Room 1',
    onPick,
  }
  render(
    <DayBar
      axis={AXIS}
      open={[{ startMinute: h(7), endMinute: h(20), isMine: false }]}
      closed={[]}
      busy={[{ startMinute: h(12), endMinute: h(13), isMine: false }]}
      selection={{ startMinute: h(10), endMinute: h(11) }}
      label="Free until 12:00"
      pick={pick}
    />,
  )
  return { onPick, bar: screen.getByRole('group', { name: /Pick a time for Room 1/ }) }
}

describe('DayBar picker', () => {
  it('books the dragged range, snapped to the grid', () => {
    const { onPick, bar } = renderBar()

    fireEvent.pointerDown(bar, { button: 0, pointerId: 1, clientX: x(h(9, 5)) })
    fireEvent.pointerMove(bar, { pointerId: 1, clientX: x(h(10, 20)) })
    fireEvent.pointerUp(bar, { pointerId: 1, clientX: x(h(10, 20)) })

    expect(onPick).toHaveBeenCalledWith({ start: h(9), end: h(10, 30) })
  })

  it('stops a drag at the next booking', () => {
    const { onPick, bar } = renderBar()

    fireEvent.pointerDown(bar, { button: 0, pointerId: 1, clientX: x(h(11)) })
    fireEvent.pointerMove(bar, { pointerId: 1, clientX: x(h(15)) })
    fireEvent.pointerUp(bar, { pointerId: 1 })

    expect(onPick).toHaveBeenCalledWith({ start: h(11), end: h(12) })
  })

  it('books the usual length on a plain click', () => {
    const { onPick, bar } = renderBar()

    fireEvent.pointerDown(bar, { button: 0, pointerId: 1, clientX: x(h(14, 10)) })
    fireEvent.pointerUp(bar, { pointerId: 1 })

    expect(onPick).toHaveBeenCalledWith({ start: h(14), end: h(15) })
  })

  it('ignores a press on booked time', () => {
    const { onPick, bar } = renderBar()

    fireEvent.pointerDown(bar, { button: 0, pointerId: 1, clientX: x(h(12, 30)) })
    fireEvent.pointerUp(bar, { pointerId: 1 })

    expect(onPick).not.toHaveBeenCalled()
  })

  it('can be used from the keyboard, skipping booked time', () => {
    const { onPick, bar } = renderBar()

    // Focus starts the cursor at the searched time (10:00); step right past 12:00–13:00.
    fireEvent.focus(bar)
    for (let i = 0; i < 8; i++) fireEvent.keyDown(bar, { key: 'ArrowRight' })
    fireEvent.keyDown(bar, { key: 'Enter' })

    expect(onPick).toHaveBeenCalledWith({ start: h(13), end: h(14) })
  })
})

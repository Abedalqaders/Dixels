// @vitest-environment jsdom
import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { BusyStatus, BusySummary, presenceOf } from './BusyStatus'
import type { PersonBusy } from './BusyStatus'

const busy: PersonBusy = { busyDates: 1, maybeBusyDates: 0, busyTimes: [{ localStart: '2026-10-13T10:00:00', localEnd: '2026-10-13T10:30:00' }] }
const maybe: PersonBusy = {
  busyDates: 0,
  maybeBusyDates: 1,
  busyTimes: [{ localStart: '2026-10-13T10:00:00', localEnd: '2026-10-13T11:00:00', isTentative: true }],
}

describe('BusyStatus', () => {
  it('reads presence: busy wins over maybe; only a Maybe is amber "maybe"', () => {
    expect(presenceOf(undefined, 1)).toBe('free')
    expect(presenceOf(busy, 1)).toBe('busy')
    expect(presenceOf(maybe, 1)).toBe('maybe')
    expect(presenceOf({ ...busy, maybeBusyDates: 2 }, 8)).toBe('part')
  })

  it('an only-maybe colleague gets an amber "Maybe busy" pill with the times', () => {
    render(<BusyStatus busy={maybe} dates={1} />)
    const pill = screen.getByRole('button', { name: /Maybe busy\W+10:00\W+–\W+11:00/ })
    expect(pill.className).toContain('state-expired-soft')
  })

  it('a busy pill lists only the firm times, and on a series adds "maybe on N more"', () => {
    const both: PersonBusy = {
      busyDates: 2,
      maybeBusyDates: 1,
      busyTimes: [
        { localStart: '2026-10-13T10:00:00', localEnd: '2026-10-13T10:30:00' },
        { localStart: '2026-10-20T10:00:00', localEnd: '2026-10-20T10:30:00' },
        { localStart: '2026-10-27T10:00:00', localEnd: '2026-10-27T10:30:00', isTentative: true },
      ],
    }
    render(<BusyStatus busy={both} dates={8} />)
    expect(screen.getByRole('button', { name: 'Busy on 2 of 8 dates · maybe on 1 more' })).toBeInTheDocument()
  })

  it('sums up: "2 people are busy, 1 might be", or amber "1 person might be busy" when it is only maybe', () => {
    const { rerender } = render(<BusySummary busy={[busy, busy, maybe]} dates={1} start="2026-10-13T10:00:00" />)
    expect(screen.getByRole('status')).toHaveTextContent('2 people are busy, 1 might be at 10:00')

    rerender(<BusySummary busy={[maybe]} dates={1} start="2026-10-13T10:00:00" />)
    expect(screen.getByRole('status')).toHaveTextContent('1 person might be busy at 10:00')
    expect(screen.getByRole('status').className).toContain('state-expired-soft')
  })
})

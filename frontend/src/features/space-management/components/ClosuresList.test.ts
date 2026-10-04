import { describe, expect, it } from 'vitest'
import { setLanguage } from '@/i18n'
import { formatWhen } from './ClosuresList'

// Local-time (no "Z") ISO strings on purpose: formatWhen reads hours/minutes in the
// *viewer's* local time (documented limitation, see its own doc comment), so a string
// without a timezone designator parses as local time — making "is this exact local
// midnight" deterministic here regardless of which timezone the test runner itself is in.
// Real API data is UTC ("Z"-suffixed); this tests the formatting algorithm itself, not the
// timezone-conversion behavior (which is a known, documented simplification, not this
// function's job to get exactly right).

describe('formatWhen', () => {
  it('shows a single date for a same-day midnight-to-midnight closure', () => {
    expect(formatWhen('2026-12-25T00:00:00', '2026-12-26T00:00:00')).toBe('25 Dec')
  })

  it('shows a date range for a multi-day midnight-to-midnight closure', () => {
    expect(formatWhen('2026-12-25T00:00:00', '2026-12-28T00:00:00')).toBe('25 Dec – 27 Dec')
  })

  it('falls back to full date+time when the window has real time-of-day precision', () => {
    const result = formatWhen('2026-10-05T09:00:00', '2026-10-05T13:00:00')
    expect(result).toContain('5 Oct')
    expect(result).toContain('→')
  })

  it('falls back to full date+time when only the start is midnight (a real time boundary)', () => {
    const result = formatWhen('2026-10-05T00:00:00', '2026-10-05T13:00:00')
    expect(result).toContain('→')
  })
})

describe('formatWhen in Arabic', () => {
  it('uses Arabic month names, and the arrow points the way Arabic reads', async () => {
    await setLanguage('ar')
    expect(formatWhen('2026-12-25T00:00:00', '2026-12-26T00:00:00')).toBe('25 ديسمبر')
    expect(formatWhen('2026-10-05T09:00:00', '2026-10-05T13:00:00')).toBe('5 أكتوبر 09:00 ← 5 أكتوبر 13:00')
  })
})

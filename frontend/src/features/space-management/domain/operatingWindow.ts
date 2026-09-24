// Port of backend/src/Dixels.Domain/SpaceManagement/ValueObjects/OperatingWindow.cs
//
// An operating-hours window on a 24-hour clock. `isOpen24Hours` is an explicit flag
// rather than being inferred from open === close — that inference was ambiguous
// (indistinguishable from a lazy default or a data-entry mistake). When
// `isOpen24Hours` is false and `close` is earlier than `open`, the window wraps past
// midnight (e.g. 22:00-02:00) — a real, supported case (reception desks, night-shift
// studios). Times are "HH:mm" strings, matching shared/test-fixtures' JSON shape.

const MINUTES_PER_DAY = 24 * 60

export class OperatingWindow {
  static readonly FullDay = new OperatingWindow('00:00', '00:00', true)

  readonly open: string
  readonly close: string
  readonly isOpen24Hours: boolean

  constructor(open: string, close: string, isOpen24Hours = false) {
    if (!isOpen24Hours && open === close) {
      throw new RangeError(
        "Open and close can't be equal for a non-24-hour window — use OperatingWindow.FullDay to express 24 hours explicitly.",
      )
    }
    this.open = open
    this.close = close
    this.isOpen24Hours = isOpen24Hours
  }

  /**
   * True if every instant this window covers is also covered by `parent`. Rotates both
   * windows so parent.open maps to minute 0 — this turns containment into a plain
   * forward-sweep comparison regardless of whether either window wraps past midnight in
   * absolute clock time. See shared/test-fixtures/operating-window-cases.json for the
   * canonical case table this is tested against.
   */
  isSubsetOf(parent: OperatingWindow): boolean {
    if (parent.isOpen24Hours) return true
    if (this.isOpen24Hours) return false

    const parentOpenMinutes = toMinutes(parent.open)
    const parentLength = mod(toMinutes(parent.close) - parentOpenMinutes)
    const childStart = mod(toMinutes(this.open) - parentOpenMinutes)
    const childEnd = mod(toMinutes(this.close) - parentOpenMinutes)

    if (childStart > childEnd) {
      // The child's rotated sweep itself wraps, so it necessarily exits the parent's
      // arc (the parent-is-full-24h case was already handled above).
      return false
    }

    return childEnd <= parentLength
  }
}

function toMinutes(time: string): number {
  const [hours, minutes] = time.split(':').map(Number)
  return hours * 60 + minutes
}

function mod(value: number): number {
  return ((value % MINUTES_PER_DAY) + MINUTES_PER_DAY) % MINUTES_PER_DAY
}

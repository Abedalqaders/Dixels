// Port of backend/src/Dixels.Domain/SpaceManagement/ValueObjects/OperatingDays.cs
//
// A set of weekdays, stored as a 7-bit mask. Bit N corresponds exactly to the BCL's
// DayOfWeek numbering (Sunday = 0 ... Saturday = 6), which is also JS's own
// Date.getDay() convention — that is what pins "which bit is Sunday" for every caller,
// in both the backend and the frontend.

import i18n from '@/i18n'
import { addDays } from '@/lib/time/buildingTime'
import { formatWeekday } from '@/lib/time/format'

export type DayName = 'Sunday' | 'Monday' | 'Tuesday' | 'Wednesday' | 'Thursday' | 'Friday' | 'Saturday'

const DAY_ORDER: readonly DayName[] = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday']

export class OperatingDays {
  static readonly AllDaysMask = 0b111_1111
  static readonly Everyday = new OperatingDays(OperatingDays.AllDaysMask)
  static readonly None = new OperatingDays(0)

  readonly mask: number

  constructor(mask: number) {
    if (mask < 0 || mask > OperatingDays.AllDaysMask) {
      throw new RangeError(`Operating days mask must be between 0 and 127 (7 bits, Sunday..Saturday). Received ${mask}.`)
    }
    this.mask = mask
  }

  static fromDayNames(days: Iterable<DayName>): OperatingDays {
    let mask = 0
    for (const day of days) {
      mask |= 1 << DAY_ORDER.indexOf(day)
    }
    return new OperatingDays(mask)
  }

  contains(day: DayName): boolean {
    return (this.mask & (1 << DAY_ORDER.indexOf(day))) !== 0
  }

  /**
   * True if every day this covers is also covered by `parent` — the narrow-only rule: a
   * child can restrict which days it's reachable further than its parent, never grant a
   * day the parent doesn't have.
   */
  isSubsetOf(parent: OperatingDays): boolean {
    return (this.mask & ~parent.mask) === 0
  }

  toDayNames(): DayName[] {
    return DAY_ORDER.filter((_, i) => (this.mask & (1 << i)) !== 0)
  }
}

/**
 * Which days a picker should let the admin select, given the resolved parent value — the
 * narrow-only rule means this is exactly `parentDays`' own set, nothing more. Named for the
 * call site (`allowedDays(resolvedParentDays)`) rather than reusing `toDayNames()` directly,
 * so a picker component reads as "what am I allowed to pick" rather than "what does this
 * value contain".
 */
export function allowedDays(parentDays: OperatingDays): DayName[] {
  return parentDays.toDayNames()
}

// ---- display (not part of the backend port) ----

/** A week known to start on a Sunday (4 Jan 2026), so day N of it is DayOfWeek N. */
const A_SUNDAY = '2026-01-04'

/** "Mon" in English; in Arabic the full name ("الاثنين"), which has no short form. */
export function dayLabel(day: DayName, style: 'long' | 'short' = 'short'): string {
  return formatWeekday(addDays(A_SUNDAY, DAY_ORDER.indexOf(day)), style)
}

/** "Every day", "None", or the days by short name: "Mon, Tue, Wed". */
export function describeDays(days: OperatingDays): string {
  const names = days.toDayNames()
  if (names.length === 7) return i18n.t('Rules:EveryDay')
  if (names.length === 0) return i18n.t('Rules:NoDays')
  return names.map((n) => dayLabel(n)).join(i18n.t('Rules:ListSeparator'))
}

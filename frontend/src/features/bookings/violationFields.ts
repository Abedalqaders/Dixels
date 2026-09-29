import type { BookingViolationDto } from '@/features/bookings/api/bookingsApi'

/**
 * Which field of the booking form a broken rule is about, so the message can sit under
 * that field instead of in one panel at the bottom. Rules that fit no single field (or
 * codes this list doesn't know yet) stay in the panel.
 */
export type IssueField = 'date' | 'time' | 'attendees' | 'other'

export interface FieldIssues {
  date: BookingViolationDto[]
  time: BookingViolationDto[]
  attendees: BookingViolationDto[]
  other: BookingViolationDto[]
}

export const NO_ISSUES: FieldIssues = { date: [], time: [], attendees: [], other: [] }

// Mirrors DixelsDomainErrorCodes (Bookings) on the server.
const FIELD_BY_CODE: Record<string, IssueField> = {
  'Dixels:Bookings:ClosedDay': 'date',
  'Dixels:Bookings:BeyondHorizon': 'date',
  'Dixels:Bookings:OutsideHours': 'time',
  'Dixels:Bookings:TooLong': 'time',
  'Dixels:Bookings:NotAligned': 'time',
  'Dixels:Bookings:StartInPast': 'time',
  'Dixels:Bookings:TooSoon': 'time',
  'Dixels:Bookings:Overlap': 'time',
  'Dixels:Bookings:OwnOverlap': 'time',
  'Dixels:Bookings:SpaceClosed': 'time',
  'Dixels:Bookings:OverCapacity': 'attendees',
  'Dixels:Bookings:BelowMinAttendees': 'attendees',
  'Dixels:Bookings:AttendeesMustBePositive': 'attendees',
}

export function fieldFor(code: string): IssueField {
  return FIELD_BY_CODE[code] ?? 'other'
}

/** The violations by field, each list in the order the server gave (most fundamental first). */
export function groupViolations(violations: BookingViolationDto[]): FieldIssues {
  const issues: FieldIssues = { date: [], time: [], attendees: [], other: [] }
  for (const v of violations) issues[fieldFor(v.code)].push(v)
  return issues
}

/** One line for under a field: the messages, in order. */
export function issueText(violations: BookingViolationDto[]): string | null {
  return violations.length ? violations.map((v) => v.message).join(' ') : null
}

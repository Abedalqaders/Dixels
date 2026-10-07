import type { PickedPerson } from '@/components/PeoplePicker'
import type { BookingInviteeDto, InviteeDto } from '@/features/bookings/api/bookingsApi'

/** At most this many guests per booking (BookingConsts.MaxInvitees). */
export const MAX_INVITEES = 50

/** Someone invited, as the booking form and Edit guests hold them. */
export type Invitee = PickedPerson

/** The guests as the API takes them: a colleague by id, anyone else by email (and name, if given). */
export function toInviteeDtos(invitees: Invitee[]): InviteeDto[] {
  return invitees.map((p) =>
    p.isExternal || !p.userId ? { email: p.email.trim(), name: p.name.trim() || null } : { userId: p.userId },
  )
}

/** The server's guest list in the form's shape. */
export function fromInviteeDtos(invitees: BookingInviteeDto[] | null | undefined): Invitee[] {
  return (invitees ?? []).map((i) => ({ userId: i.userId ?? null, name: i.name, email: i.email, isExternal: i.isExternal }))
}

/**
 * The head count once `inviteeCount` people are invited: at least you plus each of them.
 * Never lowered — a higher number may count people who aren't named. An empty or broken
 * number becomes the least that fits.
 */
export function headCountFor(attendees: number, inviteeCount: number): number {
  const least = 1 + inviteeCount
  return Number.isFinite(attendees) ? Math.max(attendees, least) : least
}

/**
 * The form's guests after the server resolved them (a preview answers with the list it
 * would save): an email that belongs to a colleague comes back as that colleague. Returns
 * `current` itself when nothing changed — so a caller adjusting state while rendering
 * stops there — or when the answer is for a different list.
 */
export function resolvedInvitees(current: Invitee[], resolved: BookingInviteeDto[] | null | undefined): Invitee[] {
  if (!resolved || resolved.length !== current.length) return current
  let changed = false
  const next = current.map((p, i) => {
    const r = resolved[i]
    const same = p.isExternal === r.isExternal && (p.userId ?? null) === (r.userId ?? null)
    if (same) return p
    // Only a guest turning into a colleague is a resolution; anything else means the answer
    // isn't about this list.
    if (!(p.isExternal && !r.isExternal && r.userId && r.email.toLowerCase() === p.email.trim().toLowerCase())) return null
    changed = true
    return { userId: r.userId, name: r.name, email: r.email, isExternal: false }
  })
  if (next.some((p) => p === null)) return current
  return changed ? (next as Invitee[]) : current
}

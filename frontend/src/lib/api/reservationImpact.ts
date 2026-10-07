// What an admin's change would do to reservations people hold — asked before saving rules,
// adding a closure, deleting a room/floor/building or moving someone to another building.
// Generic on purpose: the backend gathers it from every module that holds reservations
// (bookings today; parking or visits later), and `kind` says which module each one is from.

/** The module an affected reservation belongs to (mirrors the backend's ReservationKinds). */
import type { ApiResponse } from '@/lib/api/schemaTypes'

export const ReservationKinds = { booking: 'booking' } as const

/**
 * One upcoming reservation an admin's change would leave behind.
 *
 * - `kind`: Which module holds it: one of ReservationKinds.
 * - `heldBy`: Who holds it.
 * - `placeName`: The room (or spot) itself.
 * - `placeDetail`: Where, more broadly: the floor it's on.
 * - `reasons`: Why it no longer fits, a few words each ("Open 09:00–17:00 only").
 */
export type AffectedReservationDto = ApiResponse<'Dixels.Reservations.AffectedReservationDto'>

/**
 * - `count`: How many in all — what keep or cancel acts on.
 * - `items`: One page of them, soonest first: ask again with `skip` = how many are shown for the next.
 * - `assignedEmployees`: Deleting a building: employees assigned to it — they can't book until reassigned.
 */
export type ReservationImpactDto = ApiResponse<'Dixels.Reservations.ReservationImpactDto'>

/** The query string for a page of a preview after the first (`?skip=50`); the first page needs none. */
export function pageQuery(skip: number): string {
  return skip > 0 ? `?skip=${skip}` : ''
}

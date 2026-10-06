// What an admin's change would do to reservations people hold — asked before saving rules,
// adding a closure, deleting a room/floor/building or moving someone to another building.
// Generic on purpose: the backend gathers it from every module that holds reservations
// (bookings today; parking or visits later), and `kind` says which module each one is from.

/** The module an affected reservation belongs to (mirrors the backend's ReservationKinds). */
export const ReservationKinds = { booking: 'booking' } as const

/** One upcoming reservation an admin's change would leave behind. */
export interface AffectedReservationDto {
  /** Which module holds it: one of ReservationKinds. */
  kind: string
  id: string
  title: string
  /** Who holds it. */
  heldBy: string
  /** The room (or spot) itself. */
  placeName: string
  /** Where, more broadly: the floor it's on. */
  placeDetail: string
  localStart: string
  localEnd: string
  /** Why it no longer fits, a few words each ("Open 09:00–17:00 only"). */
  reasons: string[]
}

export interface ReservationImpactDto {
  count: number
  items: AffectedReservationDto[]
  /** Deleting a building: employees assigned to it — they can't book until reassigned. */
  assignedEmployees?: number
}

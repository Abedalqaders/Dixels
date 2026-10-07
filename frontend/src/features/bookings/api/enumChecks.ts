/**
 * The app's enum objects (`CancelScope.This` and the like) against the API's enums, both
 * ways: a value the backend dropped fails the `satisfies` next to each object; one it added
 * fails here, naming the enum. Types only — nothing in this file reaches the bundle.
 */
import type { Check, NamesEvery } from '@/lib/api/schemaTypes'
import type * as Bookings from './bookingsApi'

export type EnumObjectsAreComplete = [
  Check<NamesEvery<typeof Bookings.CancelScope, Bookings.CancelScope>>,
  Check<NamesEvery<typeof Bookings.RecurrenceFrequency, Bookings.RecurrenceFrequency>>,
  Check<NamesEvery<typeof Bookings.MonthlyRepeat, Bookings.MonthlyRepeat>>,
]

/**
 * The app's enum objects (`OwnOverlapPolicy.Warn` and the like) against the API's enums, both
 * ways: a value the backend dropped fails the `satisfies` next to each object; one it added
 * fails here, naming the enum. Types only — nothing in this file reaches the bundle.
 */
import type { Check, NamesEvery } from './schemaTypes'
import type { CancelScope, InviteeResponseStatus, MonthlyRepeat, RecurrenceFrequency } from '@/features/bookings/api/bookingsApi'
import type {
  IconKey,
  OverrideEffect,
  OverrideScope,
  OwnOverlapPolicy,
  ReasonCategory,
} from '@/features/space-management/api/spaceManagementApi'
import type * as Bookings from '@/features/bookings/api/bookingsApi'
import type * as SpaceManagement from '@/features/space-management/api/spaceManagementApi'

export type EnumObjectsAreComplete = [
  Check<NamesEvery<typeof Bookings.CancelScope, CancelScope>>,
  Check<NamesEvery<typeof Bookings.RecurrenceFrequency, RecurrenceFrequency>>,
  Check<NamesEvery<typeof Bookings.MonthlyRepeat, MonthlyRepeat>>,
  Check<NamesEvery<typeof Bookings.InviteeResponseStatus, InviteeResponseStatus>>,
  Check<NamesEvery<typeof SpaceManagement.IconKey, IconKey>>,
  Check<NamesEvery<typeof SpaceManagement.OwnOverlapPolicy, OwnOverlapPolicy>>,
  Check<NamesEvery<typeof SpaceManagement.OverrideScope, OverrideScope>>,
  Check<NamesEvery<typeof SpaceManagement.OverrideEffect, OverrideEffect>>,
  Check<NamesEvery<typeof SpaceManagement.ReasonCategory, ReasonCategory>>,
]

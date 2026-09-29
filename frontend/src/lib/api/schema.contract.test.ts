/**
 * The hand-written DTO types against the API's own description (src/lib/api/schema.d.ts,
 * generated from the backend's Swagger by `npm run api:types`). This file is type-checked
 * with the rest of the tests: a field renamed, removed, or retyped on the backend fails
 * `tsc` here, naming the field, before anything reaches a browser.
 *
 * Shapes are compared with optionality and null stripped on both sides (`Loose`): OpenAPI
 * 3.0 cannot say "nullable" on a `$ref`, and ABP always serialises every property, so
 * `x?: T | null` and `x: T | null` are the same wire contract. What is compared is what
 * matters — the set of fields and each field's underlying type.
 */
import { describe, expect, it } from 'vitest'
import type { components } from './schema'
import type {
  AffectedBookingDto,
  AvailabilityOverrideDto,
  BookingImpactDto,
  BuildingDto,
  ConstraintsSaveResultDto,
  CreateAvailabilityOverrideDto,
  CreateBuildingDto,
  CreateFloorDto,
  CreateSpaceDto,
  CreateSpaceTypeDto,
  FloorDto,
  OperatingWindowDto,
  ResolvedConstraintsDto,
  SpaceDto,
  SpaceTypeDto,
  UpdateBuildingConstraintsDto,
  UpdateBuildingDto,
  UpdateFloorConstraintsDto,
  UpdateFloorDto,
  UpdateSpaceConstraintsDto,
  UpdateSpaceDto,
  UpdateSpaceTypeDto,
} from '@/features/space-management/api/spaceManagementApi'
import type {
  AvailabilitySearchResultDto,
  BookableBuildingDto,
  BookableFloorDto,
  BookableSpaceDto,
  BookingDto,
  BookingPreviewDto,
  BookingRequestDto,
  BookingSummaryDto,
  BookingViolationDto,
  CreateBookingDto,
  CreateSeriesDto,
  OccurrencePreviewDto,
  RecurrenceDto,
  SeriesCreatedDto,
  SeriesPreviewDto,
  SeriesRequestDto,
  SpaceAvailabilityDto,
} from '@/features/bookings/api/bookingsApi'
import type { UserRolesDto } from '@/features/users/api/usersApi'

type S = components['schemas']

/** Optionality and null removed, recursively — see the note at the top. */
type Loose<T> = T extends (infer U)[]
  ? Loose<U>[]
  : T extends object
    ? { [K in keyof T]-?: Loose<Exclude<T[K], null | undefined>> }
    : T

/**
 * Every field where the two sides disagree, with both versions — an empty object when they
 * agree. Assigning `{}` to it makes tsc name the first differing field in its message.
 */
type Diff<Frontend, Api> = {
  [K in keyof Frontend as K extends keyof Api
    ? Frontend[K] extends Api[K]
      ? Api[K] extends Frontend[K]
        ? never
        : K
      : K
    : K]: { frontend: Frontend[K]; api: K extends keyof Api ? Api[K] : 'not in the API' }
} & {
  [K in Exclude<keyof Api, keyof Frontend>]: { frontend: 'missing'; api: Api[K] }
}

type Same<Frontend, Api> = Diff<Loose<Frontend>, Loose<Api>>

// ---- Responses: the frontend type must match the API exactly ----
void ({} satisfies Same<BookingDto, S['Dixels.Bookings.BookingDto']>)
void ({} satisfies Same<BookingSummaryDto, S['Dixels.Bookings.BookingSummaryDto']>)
void ({} satisfies Same<BookableBuildingDto, S['Dixels.Bookings.BookableBuildingDto']>)
void ({} satisfies Same<BookableFloorDto, S['Dixels.Bookings.BookableFloorDto']>)
void ({} satisfies Same<BookableSpaceDto, S['Dixels.Bookings.BookableSpaceDto']>)
void ({} satisfies Same<SpaceAvailabilityDto, S['Dixels.Bookings.SpaceAvailabilityDto']>)
void ({} satisfies Same<AvailabilitySearchResultDto, S['Dixels.Bookings.AvailabilitySearchResultDto']>)
void ({} satisfies Same<BookingPreviewDto, S['Dixels.Bookings.BookingPreviewDto']>)
void ({} satisfies Same<BookingViolationDto, S['Dixels.Bookings.BookingViolationDto']>)
void ({} satisfies Same<RecurrenceDto, S['Dixels.Bookings.RecurrenceDto']>)
void ({} satisfies Same<OccurrencePreviewDto, S['Dixels.Bookings.OccurrencePreviewDto']>)
void ({} satisfies Same<SeriesPreviewDto, S['Dixels.Bookings.SeriesPreviewDto']>)
void ({} satisfies Same<SeriesCreatedDto, S['Dixels.Bookings.SeriesCreatedDto']>)
void ({} satisfies Same<BookingImpactDto, S['Dixels.Bookings.BookingImpactDto']>)
void ({} satisfies Same<AffectedBookingDto, S['Dixels.Bookings.AffectedBookingDto']>)
void ({} satisfies Same<BuildingDto, S['Dixels.SpaceManagement.BuildingDto']>)
void ({} satisfies Same<FloorDto, S['Dixels.SpaceManagement.FloorDto']>)
void ({} satisfies Same<SpaceDto, S['Dixels.SpaceManagement.SpaceDto']>)
void ({} satisfies Same<SpaceTypeDto, S['Dixels.SpaceManagement.SpaceTypeDto']>)
void ({} satisfies Same<AvailabilityOverrideDto, S['Dixels.SpaceManagement.AvailabilityOverrideDto']>)
void ({} satisfies Same<OperatingWindowDto, S['Dixels.SpaceManagement.OperatingWindowDto']>)
void ({} satisfies Same<ResolvedConstraintsDto, S['Dixels.SpaceManagement.ResolvedConstraintsDto']>)
void ({} satisfies Same<ConstraintsSaveResultDto, S['Dixels.SpaceManagement.ConstraintsSaveResultDto']>)
void ({} satisfies Same<UserRolesDto, S['Dixels.Users.UserRolesDto']>)

// ---- Requests: what the frontend sends must be what the API accepts ----
void ({} satisfies Same<BookingRequestDto, S['Dixels.Bookings.BookingRequestDto']>)
void ({} satisfies Same<CreateBookingDto, S['Dixels.Bookings.CreateBookingDto']>)
void ({} satisfies Same<SeriesRequestDto, S['Dixels.Bookings.SeriesRequestDto']>)
void ({} satisfies Same<CreateSeriesDto, S['Dixels.Bookings.CreateSeriesDto']>)
void ({} satisfies Same<CreateBuildingDto, S['Dixels.SpaceManagement.CreateBuildingDto']>)
void ({} satisfies Same<UpdateBuildingDto, S['Dixels.SpaceManagement.UpdateBuildingDto']>)
void ({} satisfies Same<UpdateBuildingConstraintsDto, S['Dixels.SpaceManagement.UpdateBuildingConstraintsDto']>)
void ({} satisfies Same<CreateFloorDto, S['Dixels.SpaceManagement.CreateFloorDto']>)
void ({} satisfies Same<UpdateFloorDto, S['Dixels.SpaceManagement.UpdateFloorDto']>)
void ({} satisfies Same<UpdateFloorConstraintsDto, S['Dixels.SpaceManagement.UpdateFloorConstraintsDto']>)
void ({} satisfies Same<CreateSpaceDto, S['Dixels.SpaceManagement.CreateSpaceDto']>)
void ({} satisfies Same<UpdateSpaceDto, S['Dixels.SpaceManagement.UpdateSpaceDto']>)
void ({} satisfies Same<UpdateSpaceConstraintsDto, S['Dixels.SpaceManagement.UpdateSpaceConstraintsDto']>)
void ({} satisfies Same<CreateSpaceTypeDto, S['Dixels.SpaceManagement.CreateSpaceTypeDto']>)
void ({} satisfies Same<UpdateSpaceTypeDto, S['Dixels.SpaceManagement.UpdateSpaceTypeDto']>)
void ({} satisfies Same<CreateAvailabilityOverrideDto, S['Dixels.SpaceManagement.CreateAvailabilityOverrideDto']>)

describe('API contract', () => {
  it('is enforced by the type checker (see the `satisfies` checks above)', () => {
    expect(true).toBe(true)
  })
})

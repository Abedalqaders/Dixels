import { ApiError, query, request } from '../../../lib/api/httpClient'
import type { FieldValueDto, OperatingWindowDto } from '../../space-management/api/spaceManagementApi'

export { ApiError }

// ---- DTOs (mirror Dixels.Application.Contracts/Bookings/Dtos) ----

export interface BookableSpaceDto {
  id: string
  name: string
  spaceTypeId: string
  spaceTypeName: string
  /** Backend IconKey enum ordinal — see iconKeyToIconName. */
  iconKey: number
  capacity: number
  minAttendees: number | null
  days: FieldValueDto<number[]>
  hours: FieldValueDto<OperatingWindowDto>
  maxDurationMinutes: FieldValueDto<number>
}

export interface BookableFloorDto {
  id: string
  name: string
  floorNumber: number | null
  spaces: BookableSpaceDto[]
}

export interface BookableBuildingDto {
  id: string
  name: string
  timezone: string
  maxHorizonDays: number
  minLeadMinutes: number
  slotMinutes: number
  floors: BookableFloorDto[]
}

/**
 * `localStart`/`localEnd` are wall-clock times in the building's timezone, with no offset
 * ("2026-09-29T10:00:00") — the server converts them using the building's zone.
 */
export interface BookingRequestDto {
  spaceId: string
  localStart: string
  localEnd: string
  attendees: number
  title?: string | null
}

export interface CreateBookingDto extends BookingRequestDto {
  idempotencyKey: string
}

export interface BookingViolationDto {
  code: string
  level: string | null
  /** Localized and ready to show. */
  message: string
  /** A few words for tight spaces, e.g. "Seats 1 — you need 4". */
  shortMessage: string
}

export interface BookingPreviewDto {
  isValid: boolean
  violations: BookingViolationDto[]
  startsAt: string
  endsAt: string
  timezone: string
}

export interface BookingDto {
  id: string
  spaceId: string
  spaceName: string
  floorName: string
  buildingName: string
  timezone: string
  startsAt: string
  endsAt: string
  localStart: string
  localEnd: string
  attendees: number
  title: string
  status: string
}

/** A stretch of the searched day, in minutes from the building's local midnight (0–1440). */
export interface DayRangeDto {
  startMinute: number
  endMinute: number
  /** Busy ranges only: it's your own booking. Everyone else's is anonymous. */
  isMine: boolean
}

export interface SpaceAvailabilityDto {
  space: BookableSpaceDto
  floorId: string
  floorName: string
  isAvailable: boolean
  violations: BookingViolationDto[]
  /** "HH:mm" the free stretch lasts until ("24:00" = midnight) — when available. */
  freeUntil: string | null
  /** The next "HH:mm" start that day where the same length fits — when busy only because of the time. */
  nextFreeStart: string | null
  open: DayRangeDto[]
  closed: DayRangeDto[]
  busy: DayRangeDto[]
}

export interface AvailabilitySearchResultDto {
  buildingId: string
  buildingName: string
  timezone: string
  localStart: string
  localEnd: string
  /** Available spaces first. */
  spaces: SpaceAvailabilityDto[]
}

export interface SearchAvailabilityInput {
  localStart: string
  localEnd: string
  attendees: number
  floorId?: string
  spaceTypeId?: string
}

// ---- Calls ----

/** The current employee's bookable building, or null when they aren't assigned to one. */
export async function getMyBookableBuilding(token: string): Promise<BookableBuildingDto | null> {
  // ABP answers a null result with 204 No Content, which request() maps to undefined.
  return (await request<BookableBuildingDto | undefined>('/api/app/availability/my-building', token)) ?? null
}

/** A dry run of the real create: same rules, nothing reserved. */
export function previewBooking(token: string, input: BookingRequestDto): Promise<BookingPreviewDto> {
  return request<BookingPreviewDto>('/api/app/bookings/preview', token, {
    method: 'POST',
    body: JSON.stringify(input),
  })
}

export function createBooking(token: string, input: CreateBookingDto): Promise<BookingDto> {
  return request<BookingDto>('/api/app/bookings', token, {
    method: 'POST',
    body: JSON.stringify(input),
  })
}

/** Every space in my building checked against one window (same rules as a real booking). */
export function searchAvailability(token: string, input: SearchAvailabilityInput): Promise<AvailabilitySearchResultDto> {
  return request<AvailabilitySearchResultDto>(
    '/api/app/availability/search' +
      query({
        localStart: input.localStart,
        localEnd: input.localEnd,
        attendees: input.attendees,
        floorId: input.floorId,
        spaceTypeId: input.spaceTypeId,
      }),
    token,
  )
}

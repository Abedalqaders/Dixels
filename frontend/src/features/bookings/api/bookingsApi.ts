import { ApiError, query, request } from '@/lib/api/httpClient'
import type { FieldValueDto, IconKey, OperatingWindowDto, OwnOverlapPolicy } from '@/features/space-management/api/spaceManagementApi'
import type { IsoDate } from '@/lib/time/buildingTime'

export { ApiError }

// ---- DTOs (mirror Dixels.Application.Contracts/Bookings/Dtos) ----

export interface BookableSpaceDto {
  id: string
  name: string
  spaceTypeId: string
  spaceTypeName: string
  iconKey: IconKey
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
  /** The employee's building was deleted: shown with its name, nothing to book. */
  isRemoved?: boolean
  /** How far ahead a recurring booking's end date may be. */
  maxSeriesHorizonDays?: number
  minLeadMinutes: number
  /** Whether one person may hold two bookings at once here (0 Allow, 1 Warn, 2 Block). */
  ownOverlapPolicy: OwnOverlapPolicy
  slotMinutes: number
  /** The building's own opening days (0 = Sunday … 6) and hours — what My calendar shades as closed. */
  days: number[]
  hours: OperatingWindowDto
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
  /** Worth knowing but doesn't stop the booking — e.g. you already have another room then. */
  warnings?: BookingViolationDto[]
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
  /** Set when this booking is one date of a recurring series, with how the series repeats. */
  seriesId?: string | null
  recurrence?: RecurrenceDto | null
  /** An admin cancelled it (a rule change, a closure, a removed room) — shown struck through until it would have ended. */
  cancelledByAdmin?: boolean
  cancelReason?: string | null
}

/**
 * What the calendar's list carries per booking — enough to draw it, nothing more. The
 * full BookingDto is fetched when one is opened (getBooking).
 */
export interface BookingSummaryDto {
  id: string
  title: string
  localStart: string
  localEnd: string
  spaceName: string
  status: string
  seriesId?: string | null
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
  /** About the search as a whole — e.g. you already have another booking then. */
  warnings?: BookingViolationDto[]
  /** Available spaces first. */
  spaces: SpaceAvailabilityDto[]
}

/** One day of a room: when it's open, closed by an admin, and booked. */
export interface SpaceDayDto {
  date: IsoDate
  open: DayRangeDto[]
  closed: DayRangeDto[]
  busy: DayRangeDto[]
}

export interface SpaceDaysDto {
  days: SpaceDayDto[]
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
  return (await request<BookableBuildingDto | undefined>('/api/app/bookable-spaces/my-building', token)) ?? null
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
    '/api/app/bookable-spaces/search' +
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

/**
 * One room's days, `from` to `to` (both inclusive, building-local) — the server cuts the
 * range to today … the last bookable date.
 */
export function getSpaceDays(token: string, spaceId: string, from: IsoDate, to: IsoDate): Promise<SpaceDaysDto> {
  return request<SpaceDaysDto>(`/api/app/bookable-spaces/${spaceId}/days${query({ from, to })}`, token)
}

/**
 * My confirmed bookings on building-local days `from` (inclusive) to `to` (exclusive),
 * earliest first. At most 62 days per call.
 */
export async function getMyBookings(token: string, from: IsoDate, to: IsoDate): Promise<BookingSummaryDto[]> {
  const result = await request<{ items: BookingSummaryDto[] }>('/api/app/bookings/mine' + query({ from, to }), token)
  return result.items
}

/** One of my bookings in full — everything the calendar's light list leaves out. */
export function getBooking(token: string, id: string): Promise<BookingDto> {
  return request<BookingDto>(`/api/app/bookings/${id}`, token)
}

/** Cancels one of my own bookings before it starts; the slot is free the moment this returns. */
/** Which bookings of a series a cancel covers (CancelScope on the server). */
export const CancelScope = { This: 0, ThisAndFollowing: 1, Series: 2 } as const
export type CancelScope = (typeof CancelScope)[keyof typeof CancelScope]

/**
 * Cancels one of my own bookings before it starts — or, for a series, this and the following
 * ones, or every upcoming one. Returns every booking cancelled; their slots are free the
 * moment this returns.
 */
export async function cancelBooking(
  token: string,
  id: string,
  reason?: string,
  scope: CancelScope = CancelScope.This,
): Promise<BookingDto[]> {
  const result = await request<{ items: BookingDto[] }>(`/api/app/bookings/${id}/cancel`, token, {
    method: 'POST',
    body: JSON.stringify({ reason: reason?.trim() || null, scope }),
  })
  return result.items
}

// ---- Recurring bookings ----

/** How often a series repeats (RecurrenceFrequency on the server). */
export const RecurrenceFrequency = { Daily: 0, Weekly: 1, Monthly: 2 } as const
export type RecurrenceFrequency = (typeof RecurrenceFrequency)[keyof typeof RecurrenceFrequency]

/** Monthly only: the same date each month, or the same weekday position ("2nd Tuesday"). */
export const MonthlyRepeat = { OnDay: 0, OnWeekday: 1 } as const
export type MonthlyRepeat = (typeof MonthlyRepeat)[keyof typeof MonthlyRepeat]

/** How a booking repeats (RecurrenceDto on the server). */
export interface RecurrenceDto {
  frequency: RecurrenceFrequency
  interval: number
  /** Weekly only: 0 = Sunday … 6 = Saturday. */
  weekdays: number[]
  monthlyRepeat: MonthlyRepeat
  /** Last date an occurrence may fall on, "YYYY-MM-DD". */
  endDate: IsoDate
}

export interface SeriesRequestDto extends BookingRequestDto {
  recurrence: RecurrenceDto
}

export interface CreateSeriesDto extends SeriesRequestDto {
  skipDates: IsoDate[]
  idempotencyKey: string
}

export interface OccurrencePreviewDto {
  date: IsoDate
  localStart: string
  localEnd: string
  isValid: boolean
  violations: BookingViolationDto[]
  warnings: BookingViolationDto[]
}

export interface SeriesPreviewDto {
  /** Problems every date shares (too many people, too long) — nothing can be booked until they're fixed. */
  seriesViolations: BookingViolationDto[]
  occurrences: OccurrencePreviewDto[]
  bookableCount: number
  timezone: string
}

export interface SeriesCreatedDto {
  seriesId: string
  bookings: BookingDto[]
}

/** Every date of a recurring booking checked against every rule, nothing reserved. */
export function previewSeries(token: string, input: SeriesRequestDto): Promise<SeriesPreviewDto> {
  return request<SeriesPreviewDto>('/api/app/bookings/series/preview', token, {
    method: 'POST',
    body: JSON.stringify(input),
  })
}

/** Books a series' dates, minus `skipDates`, all at once — or none if one was taken meanwhile. */
export function createSeries(token: string, input: CreateSeriesDto): Promise<SeriesCreatedDto> {
  return request<SeriesCreatedDto>('/api/app/bookings/series', token, {
    method: 'POST',
    body: JSON.stringify(input),
  })
}

import { ApiError, query, request } from '@/lib/api/httpClient'
import type { ApiDto, ApiResponse } from '@/lib/api/schemaTypes'
import type { IsoDate } from '@/lib/time/buildingTime'

export { ApiError }

// ---- DTOs: the API's own types (src/lib/api/schema.d.ts, see lib/api/schemaTypes.ts) ----

/** A room the employee can book, with its rules resolved through floor and building. */
export type BookableSpaceDto = ApiResponse<'Dixels.Bookings.BookableSpaceDto'>

export type BookableFloorDto = ApiResponse<'Dixels.Bookings.BookableFloorDto'>

/**
 * The employee's building: `isRemoved` when it was deleted (shown with its name, nothing to
 * book); `ownOverlapPolicy` says whether one person may hold two bookings at once; `days`
 * (0 = Sunday … 6) and `hours` are what My calendar shades as closed.
 */
export type BookableBuildingDto = ApiResponse<'Dixels.Bookings.BookableBuildingDto'>

/**
 * `localStart`/`localEnd` are wall-clock times in the building's timezone, with no offset
 * ("2026-09-29T10:00:00") — the server converts them using the building's zone.
 */
export type BookingRequestDto = ApiDto<'Dixels.Bookings.BookingRequestDto'>

export type CreateBookingDto = ApiDto<'Dixels.Bookings.CreateBookingDto'>

/** `message` is localized and ready to show; `shortMessage` is a few words for tight spaces ("Seats 1 — you need 4"). */
export type BookingViolationDto = ApiResponse<'Dixels.Bookings.BookingViolationDto'>

/** A dry run's answer; `warnings` don't stop the booking (you already have another room then). */
export type BookingPreviewDto = ApiResponse<'Dixels.Bookings.BookingPreviewDto'>

/**
 * A booking in full. `seriesId`/`recurrence` are set when it's one date of a recurring series;
 * `cancelledByAdmin` (a rule change, a closure, a removed room) shows it struck through until
 * it would have ended.
 */
export type BookingDto = ApiResponse<'Dixels.Bookings.BookingDto'>

/**
 * What the calendar's list carries per booking — enough to draw it, nothing more. The
 * full BookingDto is fetched when one is opened (getBooking).
 */
export type BookingSummaryDto = ApiResponse<'Dixels.Bookings.BookingSummaryDto'>

// ---- Guests ----

/** One person to invite: a colleague by `userId`, or an outsider by `email` (+ an optional `name`) — exactly one of the two. */
export type InviteeDto = ApiDto<'Dixels.Bookings.InviteeDto'>

/** A guest as saved (or as a preview would save them). `email` is empty unless I own the booking. */
export type BookingInviteeDto = ApiResponse<'Dixels.Bookings.BookingInviteeDto'>

/** A guest's answer to the invitation (InviteeResponseStatus on the server); Pending until accept/decline exists. */
export const InviteeResponseStatus = { Pending: 0, Accepted: 1, Declined: 2 } as const satisfies Record<string, InviteeResponseStatus>
export type InviteeResponseStatus = ApiDto<'Dixels.Bookings.InviteeResponseStatus'>

/** The owner's new guest list and head count, for a booking or a whole series. */
export type UpdateInviteesDto = ApiDto<'Dixels.Bookings.UpdateInviteesDto'>

/** The busy ones among the colleagues asked about, and how many dates were checked ("busy on 2 of 8 dates"). */
export type BusyGuestsResultDto = ApiResponse<'Dixels.Bookings.BusyGuestsResultDto'>

/** Someone in my building the guest picker offers (`GET /api/app/colleagues`). */
export type ColleagueDto = ApiResponse<'Dixels.Users.ColleagueDto'>

/** A stretch of the searched day, in minutes from the building's local midnight (0–1440); `isMine` on busy ranges only. */
export type DayRangeDto = ApiResponse<'Dixels.Bookings.DayRangeDto'>

/**
 * One room in a search: `freeUntil` is the "HH:mm" the free stretch lasts until ("24:00" =
 * midnight) when available; `nextFreeStart` the next start that day where the same length
 * fits, when it's busy only because of the time.
 */
export type SpaceAvailabilityDto = ApiResponse<'Dixels.Bookings.SpaceAvailabilityDto'>

/** A search's answer, available spaces first; `warnings` are about the search as a whole. */
export type AvailabilitySearchResultDto = ApiResponse<'Dixels.Bookings.AvailabilitySearchResultDto'>

/** One day of a room: when it's open, closed by an admin, and booked. */
export type SpaceDayDto = ApiResponse<'Dixels.Bookings.SpaceDayDto'>

export type SpaceDaysDto = ApiResponse<'Dixels.Bookings.SpaceDaysDto'>

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

/**
 * Changes who's invited to one of my bookings that hasn't started (not a date of a series —
 * see updateSeriesInvitees). Guests who stay keep their answer.
 */
export function updateInvitees(token: string, id: string, input: UpdateInviteesDto): Promise<BookingDto> {
  return request<BookingDto>(`/api/app/bookings/${id}/invitees`, token, { method: 'PUT', body: JSON.stringify(input) })
}

/**
 * Which of these colleagues are busy at the time of one of my bookings — their own booking or
 * a meeting they accepted; this booking itself doesn't count. Never says with what.
 */
export function getBusyGuests(token: string, id: string, userIds: string[]): Promise<BusyGuestsResultDto> {
  return request<BusyGuestsResultDto>(`/api/app/bookings/${id}/busy-guests`, token, { method: 'POST', body: JSON.stringify({ userIds }) })
}

/** The same across a series' upcoming dates, each with how many of them they're busy on. */
export function getSeriesBusyGuests(token: string, seriesId: string, userIds: string[]): Promise<BusyGuestsResultDto> {
  return request<BusyGuestsResultDto>(`/api/app/bookings/series/${seriesId}/busy-guests`, token, {
    method: 'POST',
    body: JSON.stringify({ userIds }),
  })
}

/** The same for a whole series: its list and every upcoming date. Returns those dates. */
export function updateSeriesInvitees(token: string, seriesId: string, input: UpdateInviteesDto): Promise<SeriesCreatedDto> {
  return request<SeriesCreatedDto>(`/api/app/bookings/series/${seriesId}/invitees`, token, { method: 'PUT', body: JSON.stringify(input) })
}

/** Which bookings of a series a cancel covers (CancelScope on the server). */
export const CancelScope = { This: 0, ThisAndFollowing: 1, Series: 2 } as const satisfies Record<string, CancelScope>
export type CancelScope = ApiDto<'Dixels.Bookings.CancelScope'>

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
export const RecurrenceFrequency = { Daily: 0, Weekly: 1, Monthly: 2 } as const satisfies Record<string, RecurrenceFrequency>
export type RecurrenceFrequency = ApiDto<'Dixels.Bookings.RecurrenceFrequency'>

/** Monthly only: the same date each month, or the same weekday position ("2nd Tuesday"). */
export const MonthlyRepeat = { OnDay: 0, OnWeekday: 1 } as const satisfies Record<string, MonthlyRepeat>
export type MonthlyRepeat = ApiDto<'Dixels.Bookings.MonthlyRepeat'>

/** How a booking repeats: `weekdays` (weekly only) are 0 = Sunday … 6; `endDate` is the last date an occurrence may fall on. */
export type RecurrenceDto = ApiDto<'Dixels.Bookings.RecurrenceDto'>

export type SeriesRequestDto = ApiDto<'Dixels.Bookings.SeriesRequestDto'>

export type CreateSeriesDto = ApiDto<'Dixels.Bookings.CreateSeriesDto'>

export type OccurrencePreviewDto = ApiResponse<'Dixels.Bookings.OccurrencePreviewDto'>

/** Every date checked; `seriesViolations` are problems every date shares (too many people, too long) — nothing books until they're fixed. */
export type SeriesPreviewDto = ApiResponse<'Dixels.Bookings.SeriesPreviewDto'>

export type SeriesCreatedDto = ApiResponse<'Dixels.Bookings.SeriesCreatedDto'>

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

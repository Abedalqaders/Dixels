// Typed fetch wrapper for the SpaceManagement backend (Dixels.Application's auto-generated
// API controllers — see backend/src/Dixels.Application/SpaceManagement/). Takes the OIDC
// access token as a parameter rather than reaching into auth itself, so this module has no
// dependency on react-oidc-context and stays trivially testable.
//
// Route shapes mirror ABP's conventional-controller naming exactly (see the explicit
// [HttpGet]/[HttpPut]/[HttpPost] attributes on the app services for the handful of methods
// that don't match ABP's Get/Create/Update/Delete guessing) — kept in sync by hand since
// there's no generated client here, only the interfaces/DTOs.
//
// The request/ApiError/query plumbing itself lives in ../../../lib/api/httpClient — shared
// with usersApi.ts, since it's generic HTTP-client code, not space-management-specific.

import { request, query } from '@/lib/api/httpClient'
import type { ListResultDto, PagedResultDto } from '@/lib/api/httpClient'
import { pageQuery } from '@/lib/api/reservationImpact'
import type { ReservationImpactDto } from '@/lib/api/reservationImpact'
import type { ApiDto, ApiResponse } from '@/lib/api/schemaTypes'

export { ApiError } from '@/lib/api/httpClient'
export type { ValidationErrorInfo, ListResultDto, PagedResultDto } from '@/lib/api/httpClient'

/** Shared shape for the paged/searchable list endpoints (Buildings/Floors/Spaces/Space types).
 * `sorting` is left out on purpose — none of the list pages expose sortable columns yet, so
 * the backend always sorts by Name and there's nothing here to pass for it. */
export interface PagedListInput {
  filter?: string
  includeDeleted?: boolean
  skipCount?: number
  maxResultCount?: number
}

// ---- Shared shapes -------------------------------------------------------

/** A name in one language: { language: 'ar', name: 'غرفة اجتماعات' } — space types,
 * buildings, floors and spaces are all named once per language.
 *
 * - `language`: ABP culture name, one of the app's languages.
 */
export type LocalizedNameDto = ApiDto<'Dixels.Localization.LocalizedNameDto'>

export type OperatingWindowDto = ApiDto<'Dixels.SpaceManagement.OperatingWindowDto'>

/**
 * - `cancelledBookings`: How many upcoming bookings were cancelled because the admin chose to.
 */
export type ConstraintsSaveResultDto = ApiResponse<'Dixels.SpaceManagement.ConstraintsSaveResultDto'>

/** Each rule with the level it comes from (`{ value, source }`). */
export type ResolvedConstraintsDto = ApiResponse<'Dixels.SpaceManagement.ResolvedConstraintsDto'>

// ---- Buildings ------------------------------------------------------------

/** The icon a space type shows (IconKey on the server), in C# declaration order. */
export const IconKey = { MeetingRoom: 0, FocusPod: 1, Desk: 2, Generic: 3 } as const satisfies Record<string, IconKey>
export type IconKey = ApiDto<'Dixels.SpaceManagement.IconKey'>

/** Whether one person may hold two bookings at the same time in a building (OwnOverlapPolicy on the server). */
export const OwnOverlapPolicy = { Allow: 0, Warn: 1, Block: 2 } as const satisfies Record<string, OwnOverlapPolicy>
export type OwnOverlapPolicy = ApiDto<'Dixels.SpaceManagement.OwnOverlapPolicy'>

/**
 * - `name`: The name to show — the server picks it for the request's language (Accept-Language), falling back to the default language's.
 * - `names`: Every name it has, one per language — what the edit form shows.
 * - `maxSeriesHorizonDays`: How far ahead recurring bookings may run — never shorter than maxHorizonDays.
 */
export type BuildingDto = ApiResponse<'Dixels.SpaceManagement.BuildingDto'>

/**
 * - `names`: One per language; the default language's is required.
 * - `maxSeriesHorizonDays`: How far ahead recurring bookings may run; 90 days when left out.
 * - `ownOverlapPolicy`: Whether one person may hold two bookings at once here; Warn when left out.
 */
export type CreateBuildingDto = ApiDto<'Dixels.SpaceManagement.CreateBuildingDto'>

/**
 * - `names`: Every name it should have — a language left out loses its name.
 */
export type UpdateBuildingDto = ApiDto<'Dixels.SpaceManagement.UpdateBuildingDto'>

/**
 * - `cancelAffectedBookings`: Also cancel the upcoming bookings the change would break (default: keep them).
 */
export type UpdateBuildingConstraintsDto = ApiDto<'Dixels.SpaceManagement.UpdateBuildingConstraintsDto'>

export function getBuildings(token: string, input: PagedListInput = {}) {
  return request<PagedResultDto<BuildingDto>>(`/api/app/buildings${query({ ...input })}`, token)
}

export function getBuilding(token: string, id: string) {
  return request<BuildingDto>(`/api/app/buildings/${id}`, token)
}

export function createBuilding(token: string, input: CreateBuildingDto) {
  return request<BuildingDto>('/api/app/buildings', token, { method: 'POST', body: JSON.stringify(input) })
}

export function updateBuilding(token: string, id: string, input: UpdateBuildingDto) {
  return request<BuildingDto>(`/api/app/buildings/${id}`, token, { method: 'PUT', body: JSON.stringify(input) })
}

export function updateBuildingConstraints(token: string, id: string, input: UpdateBuildingConstraintsDto) {
  return request<ConstraintsSaveResultDto>(`/api/app/buildings/${id}/constraints`, token, {
    method: 'PUT',
    body: JSON.stringify(input),
  })
}

/** The upcoming bookings these proposed building rules would break — nothing is saved. */
export function getBuildingConstraintsImpact(token: string, id: string, input: UpdateBuildingConstraintsDto, skip = 0) {
  return request<ReservationImpactDto>(`/api/app/buildings/${id}/constraints/impact${pageQuery(skip)}`, token, { method: 'POST', body: JSON.stringify(input) })
}

/** The upcoming bookings deleting this building would cancel. */
export function getBuildingDeleteImpact(token: string, id: string, skip = 0) {
  return request<ReservationImpactDto>(`/api/app/buildings/${id}/delete-impact${pageQuery(skip)}`, token)
}

export function deleteBuilding(token: string, id: string) {
  return request<void>(`/api/app/buildings/${id}`, token, { method: 'DELETE' })
}

// ---- Floors -----------------------------------------------------------

/**
 * - `name`: The name to show — the server picks it for the request's language (Accept-Language), falling back to the default language's.
 * - `names`: Every name it has, one per language — what the edit form shows.
 * - `buildingName`: Set on every list result (the standalone Floors page needs it) — null from a plain single-floor fetch.
 */
export type FloorDto = ApiResponse<'Dixels.SpaceManagement.FloorDto'>

/**
 * - `names`: One per language; the default language's is required.
 */
export type CreateFloorDto = ApiDto<'Dixels.SpaceManagement.CreateFloorDto'>

/**
 * - `names`: Every name it should have — a language left out loses its name.
 */
export type UpdateFloorDto = ApiDto<'Dixels.SpaceManagement.UpdateFloorDto'>

/**
 * - `cancelAffectedBookings`: Also cancel the upcoming bookings the change would break (default: keep them).
 */
export type UpdateFloorConstraintsDto = ApiDto<'Dixels.SpaceManagement.UpdateFloorConstraintsDto'>

export interface FloorListInput extends PagedListInput {
  /** Omit to list floors across every building (the explorer's search); set to scope to
   * one building (the drill-down Floors-of-a-building page). */
  buildingId?: string
  /** Match `filter` against the floor's own name only, not its building's name. */
  floorNameOnly?: boolean
}

export function getFloors(token: string, input: FloorListInput) {
  return request<PagedResultDto<FloorDto>>(`/api/app/floors${query({ ...input })}`, token)
}

export function getFloor(token: string, id: string) {
  return request<FloorDto>(`/api/app/floors/${id}`, token)
}

export function createFloor(token: string, input: CreateFloorDto) {
  return request<FloorDto>('/api/app/floors', token, { method: 'POST', body: JSON.stringify(input) })
}

export function updateFloor(token: string, id: string, input: UpdateFloorDto) {
  return request<FloorDto>(`/api/app/floors/${id}`, token, { method: 'PUT', body: JSON.stringify(input) })
}

export function updateFloorConstraints(token: string, id: string, input: UpdateFloorConstraintsDto) {
  return request<ConstraintsSaveResultDto>(`/api/app/floors/${id}/constraints`, token, {
    method: 'PUT',
    body: JSON.stringify(input),
  })
}

export function getFloorConstraintsImpact(token: string, id: string, input: UpdateFloorConstraintsDto, skip = 0) {
  return request<ReservationImpactDto>(`/api/app/floors/${id}/constraints/impact${pageQuery(skip)}`, token, { method: 'POST', body: JSON.stringify(input) })
}

export function getFloorDeleteImpact(token: string, id: string, skip = 0) {
  return request<ReservationImpactDto>(`/api/app/floors/${id}/delete-impact${pageQuery(skip)}`, token)
}

export function getFloorResolvedConstraints(token: string, id: string) {
  return request<ResolvedConstraintsDto>(`/api/app/floors/${id}/resolved-constraints`, token)
}

export function deleteFloor(token: string, id: string) {
  return request<void>(`/api/app/floors/${id}`, token, { method: 'DELETE' })
}

// ---- Spaces -----------------------------------------------------------

/**
 * - `name`: The name to show — the server picks it for the request's language (Accept-Language), falling back to the default language's.
 * - `names`: Every name it has, one per language — what the edit form shows.
 * - `floorName`: Set on every list result (the standalone Spaces page needs these) — null from a plain single-space fetch.
 */
export type SpaceDto = ApiResponse<'Dixels.SpaceManagement.SpaceDto'>

/**
 * - `names`: One per language; the default language's is required.
 */
export type CreateSpaceDto = ApiDto<'Dixels.SpaceManagement.CreateSpaceDto'>

/**
 * - `names`: Every name it should have — a language left out loses its name.
 * - `cancelAffectedBookings`: Also cancel upcoming bookings for more people than the new capacity (default: keep them).
 */
export type UpdateSpaceDto = ApiDto<'Dixels.SpaceManagement.UpdateSpaceDto'>

/**
 * - `cancelAffectedBookings`: Also cancel the upcoming bookings the change would break (default: keep them).
 */
export type UpdateSpaceConstraintsDto = ApiDto<'Dixels.SpaceManagement.UpdateSpaceConstraintsDto'>

export interface SpaceListInput extends PagedListInput {
  /** Omit to list spaces across every floor (the standalone Spaces page); set to scope to
   * one floor (the drill-down Spaces-of-a-floor page). */
  floorId?: string
  /** Optional building-level filter, for the standalone Spaces page. */
  buildingId?: string
  spaceTypeId?: string
}

export function getSpaces(token: string, input: SpaceListInput) {
  return request<PagedResultDto<SpaceDto>>(`/api/app/spaces${query({ ...input })}`, token)
}

export function getSpace(token: string, id: string) {
  return request<SpaceDto>(`/api/app/spaces/${id}`, token)
}

export function createSpace(token: string, input: CreateSpaceDto) {
  return request<SpaceDto>('/api/app/spaces', token, { method: 'POST', body: JSON.stringify(input) })
}

export function updateSpace(token: string, id: string, input: UpdateSpaceDto) {
  return request<SpaceDto>(`/api/app/spaces/${id}`, token, { method: 'PUT', body: JSON.stringify(input) })
}

export function updateSpaceConstraints(token: string, id: string, input: UpdateSpaceConstraintsDto) {
  return request<ConstraintsSaveResultDto>(`/api/app/spaces/${id}/constraints`, token, {
    method: 'PUT',
    body: JSON.stringify(input),
  })
}

export function getSpaceConstraintsImpact(token: string, id: string, input: UpdateSpaceConstraintsDto, skip = 0) {
  return request<ReservationImpactDto>(`/api/app/spaces/${id}/constraints/impact${pageQuery(skip)}`, token, { method: 'POST', body: JSON.stringify(input) })
}

/** The upcoming bookings a room details change (a lower capacity) would break — nothing is saved. */
export function getSpaceUpdateImpact(token: string, id: string, input: UpdateSpaceDto, skip = 0) {
  return request<ReservationImpactDto>(`/api/app/spaces/${id}/impact${pageQuery(skip)}`, token, { method: 'POST', body: JSON.stringify(input) })
}

export function getSpaceDeleteImpact(token: string, id: string, skip = 0) {
  return request<ReservationImpactDto>(`/api/app/spaces/${id}/delete-impact${pageQuery(skip)}`, token)
}

export function getSpaceResolvedConstraints(token: string, id: string) {
  return request<ResolvedConstraintsDto>(`/api/app/spaces/${id}/resolved-constraints`, token)
}

export function deleteSpace(token: string, id: string) {
  return request<void>(`/api/app/spaces/${id}`, token, { method: 'DELETE' })
}

// ---- Space types --------------------------------------------------------

/**
 * - `name`: The name to show — the server picks it for the request's language (Accept-Language), falling back to the default language's.
 * - `names`: Every name it has, one per language — what the edit form shows.
 */
export type SpaceTypeDto = ApiResponse<'Dixels.SpaceManagement.SpaceTypeDto'>

/**
 * - `names`: One per language; the default language's is required.
 */
export type CreateSpaceTypeDto = ApiDto<'Dixels.SpaceManagement.CreateSpaceTypeDto'>

/**
 * - `names`: Every name the type should have — a language left out loses its name.
 */
export type UpdateSpaceTypeDto = ApiDto<'Dixels.SpaceManagement.UpdateSpaceTypeDto'>

/** ABP's largest page (LimitedResultRequestDto.MaxMaxResultCount). */
const MAX_PAGE_SIZE = 1000

/**
 * One page of space types when `input` is given; without it, every one — for pickers and
 * icons. That reads pages of ABP's largest size until the total is in: a single request for
 * any real company (tens of types), and nothing silently cut off past the 1000th.
 */
export async function getSpaceTypes(token: string, input?: Omit<PagedListInput, 'includeDeleted'>) {
  if (input) return request<PagedResultDto<SpaceTypeDto>>(`/api/app/space-types${query({ ...input })}`, token)

  const items: SpaceTypeDto[] = []
  let totalCount = 0
  do {
    const page = await request<PagedResultDto<SpaceTypeDto>>(
      `/api/app/space-types${query({ skipCount: items.length, maxResultCount: MAX_PAGE_SIZE })}`,
      token,
    )
    totalCount = page.totalCount
    items.push(...page.items)
    // An empty page means the list shrank while reading: stop rather than loop.
    if (page.items.length === 0) break
  } while (items.length < totalCount)
  return { items, totalCount: items.length }
}

export function createSpaceType(token: string, input: CreateSpaceTypeDto) {
  return request<SpaceTypeDto>('/api/app/space-types', token, { method: 'POST', body: JSON.stringify(input) })
}

export function updateSpaceType(token: string, id: string, input: UpdateSpaceTypeDto) {
  return request<SpaceTypeDto>(`/api/app/space-types/${id}`, token, { method: 'PUT', body: JSON.stringify(input) })
}

export function deleteSpaceType(token: string, id: string) {
  return request<void>(`/api/app/space-types/${id}`, token, { method: 'DELETE' })
}

// ---- Availability overrides (closures) -----------------------------------
//
// Numeric values confirmed against the live host's swagger.json (no string-enum
// converter is configured), in C# declaration order:
export const OverrideScope = { Building: 0, Floor: 1, Space: 2 } as const satisfies Record<string, OverrideScope>
export type OverrideScope = ApiDto<'Dixels.SpaceManagement.OverrideScope'>

export const OverrideEffect = { Closed: 0, Open: 1 } as const satisfies Record<string, OverrideEffect>
export type OverrideEffect = ApiDto<'Dixels.SpaceManagement.OverrideEffect'>

export const ReasonCategory = { Maintenance: 0, Holiday: 1, Event: 2, Other: 3 } as const satisfies Record<string, ReasonCategory>
export type ReasonCategory = ApiDto<'Dixels.SpaceManagement.ReasonCategory'>

export type AvailabilityOverrideDto = ApiResponse<'Dixels.SpaceManagement.AvailabilityOverrideDto'>

/**
 * - `cancelAffectedBookings`: Also cancel the upcoming bookings the change would break (default: keep them).
 */
export type CreateAvailabilityOverrideDto = ApiDto<'Dixels.SpaceManagement.CreateAvailabilityOverrideDto'>

export interface GetOverridesInput {
  /** Also the ones already over, most recent first. Default: upcoming and current only, soonest first. */
  includePast?: boolean
  skipCount?: number
  maxResultCount?: number
}

/** One page of a level's own closures — for showing them. Never for "closed now": see getActiveOverrides. */
export function getOverrides(token: string, scope: OverrideScope, scopeId: string, input: GetOverridesInput = {}) {
  return request<PagedResultDto<AvailabilityOverrideDto>>(
    `/api/app/availability-overrides${query({ scope, scopeId, ...input })}`,
    token,
  )
}

/**
 * Every closure and special opening in effect right now, unpaged — what "closed now" is
 * worked out from. A page of getOverrides could leave the current one out.
 */
export function getActiveOverrides(token: string, scope: OverrideScope, scopeId: string) {
  return request<ListResultDto<AvailabilityOverrideDto>>(
    `/api/app/availability-overrides/active${query({ scope, scopeId })}`,
    token,
  )
}

export function createOverride(token: string, input: CreateAvailabilityOverrideDto) {
  return request<AvailabilityOverrideDto>('/api/app/availability-overrides', token, {
    method: 'POST',
    body: JSON.stringify(input),
  })
}

/** The upcoming bookings this closure would fall on — nothing is saved. */
export function getOverrideImpact(token: string, input: CreateAvailabilityOverrideDto, skip = 0) {
  return request<ReservationImpactDto>(`/api/app/availability-overrides/impact${pageQuery(skip)}`, token, { method: 'POST', body: JSON.stringify(input) })
}

export function deleteOverride(token: string, id: string) {
  return request<void>(`/api/app/availability-overrides/${id}`, token, { method: 'DELETE' })
}

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
// with employeesApi.ts, since it's generic HTTP-client code, not space-management-specific.

import { request, query } from '../../../lib/api/httpClient'
import type { ListResultDto, PagedResultDto } from '../../../lib/api/httpClient'

export { ApiError } from '../../../lib/api/httpClient'
export type { ValidationErrorInfo, ListResultDto, PagedResultDto } from '../../../lib/api/httpClient'

/** Shared shape for the paged/searchable list endpoints (Buildings/Floors/Spaces).
 * `sorting` is left out on purpose — none of the list pages expose sortable columns yet, so
 * the backend always sorts by Name and there's nothing here to pass for it. */
export interface PagedListInput {
  filter?: string
  includeDeleted?: boolean
  skipCount?: number
  maxResultCount?: number
}

// ---- Shared shapes -------------------------------------------------------

export interface OperatingWindowDto {
  isOpen24Hours: boolean
  open: string
  close: string
}

export interface ConstraintsSaveResultDto {
  concurrencyStamp: string
  warnings: string[]
}

export interface FieldValueDto<T> {
  value: T
  source: string
}

export interface ResolvedConstraintsDto {
  timezone: string
  days: FieldValueDto<number[]>
  hours: FieldValueDto<OperatingWindowDto>
  maxDurationMinutes: FieldValueDto<number>
  maxHorizonDays: number
  minLeadMinutes: number
  minAttendees: number | null
  capacity: number | null
  buildingId: string
  buildingName: string
  floorId: string | null
  floorName: string | null
}

// ---- Buildings ------------------------------------------------------------

export interface BuildingDto {
  id: string
  name: string
  buildingNumber: string | null
  timezone: string
  days: number[]
  hours: OperatingWindowDto
  maxDurationMinutes: number
  maxHorizonDays: number
  minLeadMinutes: number
  isDeleted: boolean
  concurrencyStamp: string
}

export interface CreateBuildingDto {
  name: string
  buildingNumber?: string | null
  timezone: string
  days: number[]
  hours: OperatingWindowDto
  maxDurationMinutes: number
  maxHorizonDays: number
  minLeadMinutes: number
}

export interface UpdateBuildingDto {
  name: string
  buildingNumber?: string | null
  timezone: string
}

export interface UpdateBuildingConstraintsDto {
  days: number[]
  hours: OperatingWindowDto
  maxDurationMinutes: number
  maxHorizonDays: number
  minLeadMinutes: number
  concurrencyStamp: string
}

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

export function deleteBuilding(token: string, id: string) {
  return request<void>(`/api/app/buildings/${id}`, token, { method: 'DELETE' })
}

export function restoreBuilding(token: string, id: string) {
  return request<void>(`/api/app/buildings/${id}/restore`, token, { method: 'POST' })
}

// ---- Floors -----------------------------------------------------------

export interface FloorDto {
  id: string
  buildingId: string
  name: string
  /** Set on every list result (the standalone Floors page needs it) — null from a plain
   * single-floor fetch. */
  buildingName: string | null
  floorNumber: number | null
  days: number[] | null
  hours: OperatingWindowDto | null
  maxDurationMinutes: number | null
  hasOverrides: boolean
  isDeleted: boolean
  concurrencyStamp: string
}

export interface CreateFloorDto {
  buildingId: string
  name: string
  floorNumber?: number | null
}

export interface UpdateFloorDto {
  name: string
  floorNumber?: number | null
}

export interface UpdateFloorConstraintsDto {
  days?: number[] | null
  hours?: OperatingWindowDto | null
  maxDurationMinutes?: number | null
  concurrencyStamp: string
}

export interface FloorListInput extends PagedListInput {
  /** Omit to list floors across every building (the standalone Floors page); set to scope
   * to one building (the drill-down Floors-of-a-building page). */
  buildingId?: string
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

export function getFloorResolvedConstraints(token: string, id: string) {
  return request<ResolvedConstraintsDto>(`/api/app/floors/${id}/resolved-constraints`, token)
}

export function deleteFloor(token: string, id: string) {
  return request<void>(`/api/app/floors/${id}`, token, { method: 'DELETE' })
}

export function restoreFloor(token: string, id: string) {
  return request<void>(`/api/app/floors/${id}/restore`, token, { method: 'POST' })
}

// ---- Spaces -----------------------------------------------------------

export interface SpaceDto {
  id: string
  floorId: string
  name: string
  /** Set on every list result (the standalone Spaces page needs these) — null from a plain
   * single-space fetch. */
  floorName: string | null
  buildingName: string | null
  spaceTypeId: string
  capacity: number
  days: number[] | null
  hours: OperatingWindowDto | null
  maxDurationMinutes: number | null
  minAttendees: number | null
  hasOverrides: boolean
  isDeleted: boolean
  concurrencyStamp: string
}

export interface CreateSpaceDto {
  floorId: string
  name: string
  spaceTypeId: string
  capacity: number
}

export interface UpdateSpaceDto {
  name: string
  spaceTypeId: string
  capacity: number
}

export interface UpdateSpaceConstraintsDto {
  days?: number[] | null
  hours?: OperatingWindowDto | null
  maxDurationMinutes?: number | null
  minAttendees?: number | null
  concurrencyStamp: string
}

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

export function getSpaceResolvedConstraints(token: string, id: string) {
  return request<ResolvedConstraintsDto>(`/api/app/spaces/${id}/resolved-constraints`, token)
}

export function deleteSpace(token: string, id: string) {
  return request<void>(`/api/app/spaces/${id}`, token, { method: 'DELETE' })
}

export function restoreSpace(token: string, id: string) {
  return request<void>(`/api/app/spaces/${id}/restore`, token, { method: 'POST' })
}

// ---- Space types --------------------------------------------------------

export interface SpaceTypeDto {
  id: string
  name: string
  // Numeric enum ordinal (MeetingRoom=0, FocusPod=1, Desk=2, Generic=3), matching the
  // backend's default System.Text.Json enum serialization (no string-enum converter is
  // configured on the host) — not yet verified against a live server, since the one
  // running instance available this session predates these endpoints.
  iconKey: number
}

export interface CreateSpaceTypeDto {
  name: string
  iconKey: number
}

export interface UpdateSpaceTypeDto {
  name: string
  iconKey: number
}

export function getSpaceTypes(token: string) {
  return request<ListResultDto<SpaceTypeDto>>('/api/app/space-types', token)
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
export const OverrideScope = { Building: 0, Floor: 1, Space: 2 } as const
export type OverrideScope = (typeof OverrideScope)[keyof typeof OverrideScope]

export const OverrideEffect = { Closed: 0, Open: 1 } as const
export type OverrideEffect = (typeof OverrideEffect)[keyof typeof OverrideEffect]

export const ReasonCategory = { Maintenance: 0, Holiday: 1, Event: 2, Other: 3 } as const
export type ReasonCategory = (typeof ReasonCategory)[keyof typeof ReasonCategory]

export interface AvailabilityOverrideDto {
  id: string
  scope: OverrideScope
  scopeId: string
  startsAt: string
  endsAt: string
  effect: OverrideEffect
  reasonCategory: ReasonCategory
  reasonDetail: string | null
}

export interface CreateAvailabilityOverrideDto {
  scope: OverrideScope
  scopeId: string
  startsAt: string
  endsAt: string
  effect: OverrideEffect
  reasonCategory: ReasonCategory
  reasonDetail?: string | null
}

export function getOverrides(token: string, scope: OverrideScope, scopeId: string) {
  return request<ListResultDto<AvailabilityOverrideDto>>(
    `/api/app/availability-overrides${query({ scope, scopeId })}`,
    token,
  )
}

export function createOverride(token: string, input: CreateAvailabilityOverrideDto) {
  return request<AvailabilityOverrideDto>('/api/app/availability-overrides', token, {
    method: 'POST',
    body: JSON.stringify(input),
  })
}

export function deleteOverride(token: string, id: string) {
  return request<void>(`/api/app/availability-overrides/${id}`, token, { method: 'DELETE' })
}

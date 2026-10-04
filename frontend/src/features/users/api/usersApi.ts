// The user list is ABP's own Identity endpoint (/api/identity/users); the building a user
// can see is an ABP extra property on the user — extraProperties.BuildingId — which the
// backend's DixelsIdentityUserAppService also lets the list filter by. Moving someone and
// the Roles column are Dixels endpoints (/api/app/users). Same request/ApiError plumbing as
// spaceManagementApi.ts.

import { request, query } from '@/lib/api/httpClient'
import type { PagedResultDto } from '@/lib/api/httpClient'
import type { BookingImpactDto } from '@/features/space-management/api/spaceManagementApi'

export { ApiError } from '@/lib/api/httpClient'

/** Key of the building extra property — matches DixelsUserConsts.BuildingIdPropertyName. */
export const BUILDING_ID_PROPERTY = 'BuildingId'

/** Filter key for the user list's role filter — matches DixelsUserConsts.RoleFilterKey. */
const ROLE_FILTER_KEY = 'Role'

/** Filter key for the user list's permission filter — matches DixelsUserConsts.PermissionFilterKey. */
const PERMISSION_FILTER_KEY = 'Permission'

type ExtraProperties = Record<string, unknown>

/** ABP's IdentityUserDto (the fields this app uses). */
export interface IdentityUserDto {
  id: string
  userName: string
  name: string | null
  surname: string | null
  email: string
  phoneNumber: string | null
  isActive: boolean
  lockoutEnabled: boolean
  concurrencyStamp: string
  extraProperties: ExtraProperties | null
}

export interface GetUsersInput {
  /** Name/username/email search — ABP's own filter. */
  filter?: string
  /** Only users assigned to this building. Omit to return everyone. */
  buildingId?: string
  /** Only members of this role (by name). Omit for every role. */
  role?: string
  /** Only users holding this permission, through a role or directly (one of Permissions.*). */
  permission?: string
  skipCount?: number
  maxResultCount?: number
}

export function buildingIdOf(user: IdentityUserDto): string | null {
  const value = user.extraProperties?.[BUILDING_ID_PROPERTY]
  return typeof value === 'string' && value ? value : null
}

export function getUsers(token: string, { buildingId, role, permission, ...input }: GetUsersInput = {}) {
  // ABP binds ExtraProperties[Key]=value from the query string into the input's extra properties.
  const params = {
    ...input,
    [`ExtraProperties[${BUILDING_ID_PROPERTY}]`]: buildingId,
    [`ExtraProperties[${ROLE_FILTER_KEY}]`]: role,
    [`ExtraProperties[${PERMISSION_FILTER_KEY}]`]: permission,
  }
  return request<PagedResultDto<IdentityUserDto>>(`/api/identity/users${query(params)}`, token)
}

/**
 * Sets (or clears, with null) a user's building. Their upcoming bookings in the
 * building they're leaving are cancelled in the same step — they can only book in one.
 */
export function assignUserBuilding(token: string, userId: string, buildingId: string | null) {
  return request<void>(`/api/app/users/${userId}/building`, token, {
    method: 'PUT',
    body: JSON.stringify({ buildingId }),
  })
}

/** A user's upcoming bookings in their current building — what moving them would leave behind. */
export function getReassignImpact(token: string, userId: string) {
  return request<BookingImpactDto>(`/api/app/users/${userId}/reassign-impact`, token)
}

export interface UserRolesDto {
  userId: string
  roles: string[]
}

/** Each of these users' role names, one call for a page of the Users list — not one per row. */
export function getUserRoles(token: string, userIds: string[]) {
  if (userIds.length === 0) return Promise.resolve<UserRolesDto[]>([])
  const params = new URLSearchParams()
  for (const id of userIds) params.append('userIds', id)
  return request<UserRolesDto[]>(`/api/app/users/roles?${params}`, token)
}

/** Every role name in the system, for the Users page's role filter. */
export function getRoleNames(token: string) {
  return request<string[]>('/api/app/users/role-names', token)
}

// Users go through ABP's own Identity endpoints (/api/identity/users); the building a user
// can see is an ABP extra property on the user — extraProperties.BuildingId — which the
// backend's DixelsIdentityUserAppService also lets the list filter by. Only my-building is
// a Dixels endpoint. Same request/ApiError plumbing as spaceManagementApi.ts.

import { request, query } from '../../../lib/api/httpClient'
import type { PagedResultDto } from '../../../lib/api/httpClient'
import type { BuildingDto } from '../../space-management/api/spaceManagementApi'

export { ApiError } from '../../../lib/api/httpClient'

/** Key of the building extra property — matches DixelsUserConsts.BuildingIdPropertyName. */
export const BUILDING_ID_PROPERTY = 'BuildingId'

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

export interface IdentityRoleDto {
  id: string
  name: string
}

export interface GetUsersInput {
  /** Name/username/email search — ABP's own filter. */
  filter?: string
  /** Only users assigned to this building. Omit to return everyone. */
  buildingId?: string
  skipCount?: number
  maxResultCount?: number
}

export function buildingIdOf(user: IdentityUserDto): string | null {
  const value = user.extraProperties?.[BUILDING_ID_PROPERTY]
  return typeof value === 'string' && value ? value : null
}

export function getUsers(token: string, { buildingId, ...input }: GetUsersInput = {}) {
  // ABP binds ExtraProperties[Key]=value from the query string into the input's extra properties.
  const params = { ...input, [`ExtraProperties[${BUILDING_ID_PROPERTY}]`]: buildingId }
  return request<PagedResultDto<IdentityUserDto>>(`/api/identity/users${query(params)}`, token)
}

export function getUserRoles(token: string, userId: string) {
  return request<{ items: IdentityRoleDto[] }>(`/api/identity/users/${userId}/roles`, token)
}

/**
 * Sets (or, with null, clears) a user's building through ABP's own user update. That
 * endpoint replaces the whole user, so this sends the user back exactly as ABP just
 * returned it with only the building changed. roleNames is left out, which ABP reads as
 * "keep the current roles"; the concurrency stamp stops it overwriting someone else's edit.
 */
export async function assignUserBuilding(token: string, userId: string, buildingId: string | null) {
  const user = await request<IdentityUserDto>(`/api/identity/users/${userId}`, token)
  return request<IdentityUserDto>(`/api/identity/users/${userId}`, token, {
    method: 'PUT',
    body: JSON.stringify({
      userName: user.userName,
      email: user.email,
      name: user.name,
      surname: user.surname,
      phoneNumber: user.phoneNumber,
      isActive: user.isActive,
      lockoutEnabled: user.lockoutEnabled,
      concurrencyStamp: user.concurrencyStamp,
      extraProperties: { ...user.extraProperties, [BUILDING_ID_PROPERTY]: buildingId },
    }),
  })
}

/** The current user's own assigned building, or null if they haven't been assigned one
 * yet. Scoped to whoever the token belongs to — never the full admin list. */
export function getMyBuilding(token: string) {
  return request<BuildingDto | null>('/api/app/users/my-building', token)
}

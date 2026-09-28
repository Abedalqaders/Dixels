// Typed fetch wrapper for the Employees backend (Dixels.Application/Employees). Same
// request/ApiError plumbing as spaceManagementApi.ts — see ../../../lib/api/httpClient.

import { request, query } from '../../../lib/api/httpClient'
import type { PagedResultDto } from '../../../lib/api/httpClient'
import type { BuildingDto } from '../../space-management/api/spaceManagementApi'

export { ApiError } from '../../../lib/api/httpClient'

export interface EmployeeDto {
  id: string
  userName: string
  name: string | null
  surname: string | null
  email: string | null
  assignedBuildingId: string | null
  assignedBuildingName: string | null
}

export interface AssignEmployeeBuildingDto {
  buildingId: string | null
}

export interface GetEmployeesInput {
  /** Name/username/email search — matches anywhere, case-insensitive. */
  filter?: string
  /** Only employees assigned to this building. Omit to return everyone. */
  buildingId?: string
  skipCount?: number
  maxResultCount?: number
}

export function getEmployees(token: string, input: GetEmployeesInput = {}) {
  return request<PagedResultDto<EmployeeDto>>(`/api/app/employees${query({ ...input })}`, token)
}

export function assignEmployeeBuilding(token: string, employeeUserId: string, input: AssignEmployeeBuildingDto) {
  return request<EmployeeDto>(`/api/app/employees/${employeeUserId}/building`, token, {
    method: 'PUT',
    body: JSON.stringify(input),
  })
}

/** The current user's own assigned building, or null if they haven't been assigned one
 * yet. Scoped to whoever the token belongs to — an employee calling this only ever gets
 * their own building back, never the full admin list. */
export function getMyBuilding(token: string) {
  return request<BuildingDto | null>('/api/app/employees/my-building', token)
}

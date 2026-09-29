import { afterEach, describe, expect, it, vi } from 'vitest'
import { getRoleNames, getUserRoles, getUsers } from './usersApi'
import { Permissions } from '@/features/auth/permissions/permissionNames'

afterEach(() => {
  vi.restoreAllMocks()
})

function requestedParams() {
  const url = new URL(String(vi.mocked(globalThis.fetch).mock.calls[0][0]))
  return url.searchParams
}

describe('getUsers', () => {
  it('asks for the users holding a permission, alongside the building filter', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(JSON.stringify({ items: [], totalCount: 0 })))

    await getUsers('token', { permission: Permissions.Bookings.Create, buildingId: 'b1', filter: 'ana' })

    const params = requestedParams()
    expect(params.get('ExtraProperties[Permission]')).toBe('Dixels.Bookings.Create')
    expect(params.get('ExtraProperties[BuildingId]')).toBe('b1')
    expect(params.get('filter')).toBe('ana')
  })

  it('leaves the permission filter out when none is asked for', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(JSON.stringify({ items: [], totalCount: 0 })))

    await getUsers('token')

    expect(requestedParams().has('ExtraProperties[Permission]')).toBe(false)
  })

  it('asks for the role filter alongside the building filter', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(JSON.stringify({ items: [], totalCount: 0 })))

    await getUsers('token', { role: 'employee', buildingId: 'b1' })

    const params = requestedParams()
    expect(params.get('ExtraProperties[Role]')).toBe('employee')
    expect(params.get('ExtraProperties[BuildingId]')).toBe('b1')
  })
})

describe('getUserRoles', () => {
  it('repeats userIds so the server sees every id, not just the last', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(JSON.stringify([])))

    await getUserRoles('token', ['u1', 'u2'])

    expect(requestedParams().getAll('userIds')).toEqual(['u1', 'u2'])
  })

  it('skips the request entirely for an empty page', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch')

    const result = await getUserRoles('token', [])

    expect(fetchSpy).not.toHaveBeenCalled()
    expect(result).toEqual([])
  })
})

describe('getRoleNames', () => {
  it('hits the role-names endpoint', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(JSON.stringify(['admin', 'employee'])))

    const result = await getRoleNames('token')

    expect(String(vi.mocked(globalThis.fetch).mock.calls[0][0])).toContain('/api/app/users/role-names')
    expect(result).toEqual(['admin', 'employee'])
  })
})

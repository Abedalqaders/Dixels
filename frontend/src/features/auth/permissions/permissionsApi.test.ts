import { describe, expect, it } from 'vitest'
import { unknownPermissionNames } from './permissionsApi'
import { Permissions } from './permissionNames'

const allUsed = Object.values(Permissions).flatMap((group) => Object.values(group))
const defined = (...names: string[]) => Object.fromEntries(names.map((n) => [n, true]))

describe('unknownPermissionNames', () => {
  it('is empty when the backend defines every name the frontend uses', () => {
    expect(unknownPermissionNames(defined(...allUsed))).toEqual([])
  })

  it('names what the backend no longer defines — a rename on one side only', () => {
    const withoutCancel = allUsed.filter((n) => n !== Permissions.Bookings.Cancel)

    expect(unknownPermissionNames(defined(...withoutCancel))).toEqual([Permissions.Bookings.Cancel])
  })

  it('does not care about backend permissions the frontend never uses', () => {
    expect(unknownPermissionNames(defined(...allUsed, 'AbpIdentity.Roles'))).toEqual([])
  })
})

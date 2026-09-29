import { describe, expect, it } from 'vitest'
import { landingFor } from './landing'
import { Permissions } from '@/features/auth/permissions/permissionNames'

const grants = (...names: string[]) => Object.fromEntries(names.map((n) => [n, true]))

describe('landingFor', () => {
  it('lands whoever runs space management there', () => {
    expect(landingFor(grants(Permissions.Buildings.Default, Permissions.Bookings.Default))).toBe('/admin/buildings')
  })

  it('lands an employee on their calendar', () => {
    expect(landingFor(grants(Permissions.Bookings.Default, Permissions.Bookings.Create))).toBe('/my-calendar')
  })

  it('moves on to the next page they can open when one is taken away', () => {
    expect(landingFor(grants(Permissions.SpaceTypes.Default, Permissions.Identity.Users))).toBe('/admin/space-types')
    expect(landingFor(grants(Permissions.Identity.Users))).toBe('/admin/users')
  })

  it('lands a floor or space editor on the hierarchy, though they hold no building permission', () => {
    expect(landingFor(grants(Permissions.Floors.Default, Permissions.Floors.Edit))).toBe('/admin/buildings')
    expect(landingFor(grants(Permissions.Spaces.Default))).toBe('/admin/buildings')
  })

  it('has nowhere to land someone granted none of the pages', () => {
    expect(landingFor(grants())).toBeNull()
    expect(landingFor(grants(Permissions.Bookings.ManageAll))).toBeNull()
  })
})

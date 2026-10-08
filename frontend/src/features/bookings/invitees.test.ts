import { describe, expect, it } from 'vitest'
import { headCountFor, resolvedInvitees, toInviteeDtos } from './invitees'
import type { Invitee } from './invitees'

const sara: Invitee = { userId: 'u-sara', name: 'Sara Ali', email: 'sara@dixels.io', isExternal: false }
const guest: Invitee = { userId: null, name: 'Omar', email: 'omar@acme.com', isExternal: true }

describe('headCountFor', () => {
  it('raises the head count to you plus everyone invited', () => {
    expect(headCountFor(1, 2)).toBe(3)
  })

  it('keeps a higher number the booker typed, and never lowers it when someone is removed', () => {
    expect(headCountFor(6, 2)).toBe(6)
    expect(headCountFor(3, 1)).toBe(3)
  })

  it('turns an empty number into the least that fits', () => {
    expect(headCountFor(Number.NaN, 2)).toBe(3)
  })
})

describe('toInviteeDtos', () => {
  it('sends a colleague by id and a guest by email and name', () => {
    expect(toInviteeDtos([sara, guest])).toEqual([{ userId: 'u-sara' }, { email: 'omar@acme.com', name: 'Omar' }])
  })

  it('sends no name for a guest without one', () => {
    expect(toInviteeDtos([{ ...guest, name: '  ' }])).toEqual([{ email: 'omar@acme.com', name: null }])
  })
})

describe('resolvedInvitees', () => {
  it("turns a guest's email that belongs to a colleague into that colleague", () => {
    const typed: Invitee = { userId: null, name: '', email: 'Sara@Dixels.io ', isExternal: true }
    const next = resolvedInvitees([typed], [{ userId: 'u-sara', name: 'Sara Ali', email: 'sara@dixels.io', isExternal: false, responseStatus: 0, isBusy: false, busyDates: 0 }])
    expect(next).toEqual([sara])
  })

  it('returns the same list when nothing changed, so the form stops adjusting', () => {
    const current = [sara, guest]
    const next = resolvedInvitees(current, [
      { userId: 'u-sara', name: 'Sara Ali', email: 'sara@dixels.io', isExternal: false, responseStatus: 0, isBusy: false, busyDates: 0 },
      { userId: null, name: 'Omar', email: 'omar@acme.com', isExternal: true, responseStatus: 0, isBusy: false, busyDates: 0 },
    ])
    expect(next).toBe(current)
  })

  it('ignores an answer about a different list', () => {
    const current = [guest]
    expect(resolvedInvitees(current, [])).toBe(current)
    expect(resolvedInvitees(current, [{ userId: 'u-x', name: 'X', email: 'x@dixels.io', isExternal: false, responseStatus: 0, isBusy: false, busyDates: 0 }])).toBe(current)
    expect(resolvedInvitees(current, undefined)).toBe(current)
  })
})
